using VubesClub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Modules.Identity;
using VubesClub.Api.Persistence;
using BookingEntity = VubesClub.Api.Persistence.BookingRow;

namespace VubesClub.Api.Modules.Booking;

/// <summary>Kesinleşen katılım için bir kez "kaydınız alındı" e-postası gönderir. Hata katılımı etkilemez.</summary>
public sealed class BookingMailer(ClubDb db, IMailSender mail, IConfiguration configuration, ILogger<BookingMailer> logger)
{
    public async Task SendConfirmation(Guid paymentId, CancellationToken ct)
    {
        try
        {
            if (!mail.IsConfigured) return;
            var booking = await db.Set<BookingEntity>().AsNoTracking()
                .SingleOrDefaultAsync(b => b.PaymentId == paymentId && b.Status == "confirmed" && b.ConfirmationSentAt == null, ct);
            if (booking == null) return;
            var member = await db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == booking.MemberId, ct);
            if (string.IsNullOrWhiteSpace(member.Email)) return;

            // Tek gönderim: önce işaretle, başarısız olursa geri al.
            var now = DateTime.UtcNow;
            var claimed = await db.Set<BookingEntity>().Where(b => b.Id == booking.Id && b.ConfirmationSentAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ConfirmationSentAt, now), ct);
            if (claimed != 1) return;

            try
            {
                var version = await db.Set<EventVersion>().AsNoTracking().SingleAsync(v => v.Id == booking.VersionId, ct);
                var pass = await db.Set<PassRow>().AsNoTracking().SingleAsync(p => p.BookingId == booking.Id && !p.Revoked, ct);
                var payload = PassQr.Payload(pass.Nonce);
                var when = BookingEmail.When(version.Starts, version.TimeZone);
                var logo = configuration["Mail:LogoUrl"] ?? "https://api.vubesclub.com/media/Wordmark.png";
                const string cid = "event-qr";
                var image = new MailImage(cid, "vubes-qr.png", PassQr.Png(payload));
                await mail.SendAsync(member.Email, member.FullName, BookingEmail.Subject(version.Title),
                    BookingEmail.Html(member.FullName, version.Title, when, version.Meeting, logo, cid),
                    BookingEmail.Text(member.FullName, version.Title, when, version.Meeting, payload),
                    [image], ct);
            }
            catch
            {
                await db.Set<BookingEntity>().Where(b => b.Id == booking.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.ConfirmationSentAt, (DateTime?)null), CancellationToken.None);
                throw;
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "Katılım e-postası gönderilemedi ({Type})", failure.GetType().Name);
        }
    }
}
