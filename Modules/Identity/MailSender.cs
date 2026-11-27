using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

/// <summary>E-posta gönderimi; testlerde sahte uygulamayla değiştirilir.</summary>
public interface IMailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string? toName, string subject, string html, string text, CancellationToken ct);

    /// <summary>Satır içi görselli ileti (ör. QR). Desteklemeyen uygulamalar görselsiz gönderir.</summary>
    Task SendAsync(string to, string? toName, string subject, string html, string text,
        IReadOnlyList<MailImage> images, CancellationToken ct) => SendAsync(to, toName, subject, html, text, ct);
}

/// <summary>E-postaya "cid:" ile bağlanan satır içi PNG.</summary>
public sealed record MailImage(string ContentId, string FileName, byte[] Png);

/// <summary>SMTP ile e-posta gönderir (IONOS: smtp.ionos.com, 587 STARTTLS).</summary>
public sealed class MailSender(IConfiguration configuration, ILogger<MailSender> logger) : IMailSender
{
    private string? Host => configuration["Mail:SmtpHost"];
    private string? User => configuration["Mail:User"];
    private string? Password => configuration["Mail:Password"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);

    /// <summary>İletiyi kurar: gerçek alan adlı Message-ID, otomatik gönderim başlıkları, metin + HTML.</summary>
    public static MimeMessage Compose(
        IConfiguration configuration, string to, string? toName, string subject, string html, string text,
        IReadOnlyList<MailImage>? images = null)
    {
        var from = configuration["Mail:From"] ?? configuration["Mail:User"] ?? "noreply@vubesclub.com";
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Mail:FromName"] ?? "Vubes Club", from));
        message.To.Add(new MailboxAddress(toName ?? "", to));
        message.Subject = subject;
        // Gerçek bir alan adına bağlı Message-ID: varsayılan değer makine adını taşır (örn. "instance-2026…"),
        // bu da spam filtrelerinin bilinen bir işaretidir.
        var domain = from.Contains('@') ? from[(from.IndexOf('@') + 1)..] : "vubesclub.com";
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(domain);
        // Otomatik gönderilmiş işlem iletisi (RFC 3834): otomatik yanıt döngülerini ve "toplu posta" şüphesini azaltır.
        message.Headers.Add("Auto-Submitted", "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");
        if (configuration["Mail:ReplyTo"] is { Length: > 0 } replyTo)
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        var body = new BodyBuilder { HtmlBody = html, TextBody = text };
        foreach (var image in images ?? [])
        {
            var resource = body.LinkedResources.Add(image.FileName, image.Png, MimeKit.ContentType.Parse("image/png"));
            resource.ContentId = image.ContentId;
            resource.ContentDisposition = new MimeKit.ContentDisposition(MimeKit.ContentDisposition.Inline);
        }
        message.Body = body.ToMessageBody();
        return message;
    }

    public Task SendAsync(string to, string? toName, string subject, string html, string text, CancellationToken ct) =>
        SendAsync(to, toName, subject, html, text, [], ct);

    public async Task SendAsync(string to, string? toName, string subject, string html, string text,
        IReadOnlyList<MailImage> images, CancellationToken ct)
    {
        if (!IsConfigured) throw new ApiFault(503, "EMAIL_UNAVAILABLE", "E-posta gönderimi yapılandırılmadı.");

        var message = Compose(configuration, to, toName, subject, html, text, images);

        // Geçici bağlantı aksaklıklarına karşı bir kez daha denenir.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var client = new SmtpClient { Timeout = 15000 };
                var port = configuration.GetValue<int?>("Mail:SmtpPort") ?? 587;
                await client.ConnectAsync(Host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
                await client.AuthenticateAsync(User, Password, ct);
                await client.SendAsync(FormatOptions.Default, new SendRequest(message), ct);
                await client.DisconnectAsync(true, ct);
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // Alıcı adresi ve kod günlüğe yazılmaz.
                logger.LogError(failure, "Giriş kodu e-postası gönderilemedi ({Type}, deneme {Attempt})", failure.GetType().Name, attempt);
                if (attempt >= 2 || failure is AuthenticationException)
                    throw new ApiFault(503, "EMAIL_UNAVAILABLE", "Kod şu anda gönderilemedi. Biraz sonra tekrar deneyin.");
                await Task.Delay(1500, ct);
            }
        }
    }
}
