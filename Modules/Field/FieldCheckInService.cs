using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Booking;
using VubesClub.Api.Persistence;
using BookingEntity = VubesClub.Api.Persistence.BookingRow;

namespace VubesClub.Api.Modules.Field;

/// <summary>Stantta okutulan katılım QR kodunu üyeyle eşleştirir ve girişi kaydeder.</summary>
public sealed class FieldCheckInService(ClubDb db, Caller caller, TimeProvider clock)
{
    public async Task<CheckInResult> CheckIn(string? payload, CancellationToken ct)
    {
        var nonce = PassQr.Nonce(payload) ?? throw ApiFault.Invalid("QR kodu tanınmadı.");
        var now = clock.GetUtcNow().UtcDateTime;

        var scanner = await db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == caller.MemberId, ct);
        var pass = await db.Set<PassRow>().AsNoTracking().SingleOrDefaultAsync(p => p.Nonce == nonce && !p.Revoked, ct)
            ?? throw ApiFault.Missing();
        var booking = await db.Set<BookingEntity>().SingleOrDefaultAsync(b => b.Id == pass.BookingId, ct)
            ?? throw ApiFault.Missing();

        var organizer = await (from e in db.Set<ClubEvent>() join t in db.Set<Trainer>() on e.TrainerId equals t.Id
            where e.Id == booking.EventId select t.MemberId).SingleAsync(ct);
        if (!(scanner.Status == "active" && (scanner.FieldStaff || organizer == scanner.Id)))
            throw ApiFault.Forbidden();

        var version = await db.Set<EventVersion>().AsNoTracking().SingleAsync(v => v.Id == booking.VersionId, ct);
        var cancelled = await db.Set<ClubEvent>().AnyAsync(e => e.Id == booking.EventId && e.Cancelled, ct);
        if (booking.Status != "confirmed" || cancelled)
            throw ApiFault.Conflict("PASS_INACTIVE", "Katılım kartı etkin değil.");
        if (version.Ends <= now)
            throw ApiFault.Conflict("EVENT_ENDED", "Etkinlik sona erdi.");

        var member = await db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == booking.MemberId, ct);
        var outcome = "alreadyCheckedIn";
        if (booking.Attendance == "unrecorded")
        {
            booking.Attendance = "checkedIn";
            booking.CheckedInAt = now;
            await db.SaveChangesAsync(ct);
            outcome = "checkedIn";
        }

        return new CheckInResult(booking.Id, member.FullName,
            member.Avatar == null ? null : new Media(member.Avatar, null),
            booking.EventId, version.Title, outcome, booking.CheckedInAt ?? now);
    }
}
