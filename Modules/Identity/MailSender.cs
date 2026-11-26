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
}

/// <summary>SMTP ile e-posta gönderir (IONOS: smtp.ionos.com, 587 STARTTLS).</summary>
public sealed class MailSender(IConfiguration configuration, ILogger<MailSender> logger) : IMailSender
{
    private string? Host => configuration["Mail:SmtpHost"];
    private string? User => configuration["Mail:User"];
    private string? Password => configuration["Mail:Password"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);

    public async Task SendAsync(string to, string? toName, string subject, string html, string text, CancellationToken ct)
    {
        if (!IsConfigured) throw new ApiFault(503, "EMAIL_UNAVAILABLE", "E-posta gönderimi yapılandırılmadı.");

        var from = configuration["Mail:From"] ?? User!;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Mail:FromName"] ?? "Vubes Club", from));
        message.To.Add(new MailboxAddress(toName ?? "", to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

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
