using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

/// <summary>SMTP ile e-posta gönderir (IONOS: smtp.ionos.com, 587 STARTTLS).</summary>
public sealed class MailSender(IConfiguration configuration, ILogger<MailSender> logger)
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

        try
        {
            using var client = new SmtpClient { Timeout = 15000 };
            var port = configuration.GetValue<int?>("Mail:SmtpPort") ?? 587;
            await client.ConnectAsync(Host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(User, Password, ct);
            await client.SendAsync(FormatOptions.Default, new SendRequest(message), ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Alıcı adresi ve kod günlüğe yazılmaz.
            logger.LogError(failure, "Giriş kodu e-postası gönderilemedi ({Type})", failure.GetType().Name);
            throw new ApiFault(503, "EMAIL_UNAVAILABLE", "Kod şu anda gönderilemedi. Biraz sonra tekrar deneyin.");
        }
    }
}
