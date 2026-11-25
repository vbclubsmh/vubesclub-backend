using Microsoft.EntityFrameworkCore;

namespace VubesClub.Api.Infrastructure;

public static class DevelopmentSamples
{
    public static async Task RefreshTimeline(ClubDb db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(SqlFiles.Read("RefreshDevelopmentTimeline.sql"), cancellationToken);
    }

    public static Task RefreshPeerLocations(ClubDb db, Guid eventId, CancellationToken cancellationToken)
    {
        // Synthetic observations only. The signed-in Ada account is excluded.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE last_location AS observation SET recorded = CURRENT_TIMESTAMP
            FROM location_share AS sharing
            WHERE observation.share_id = sharing.id AND sharing.event_id = {eventId}
                AND sharing.event_id = md5('vubes:event:live')::uuid
                AND sharing.member_id IN (
                    '6b6b0000-0000-4000-8000-000000000002'::uuid,
                    md5('vubes:member:ece')::uuid, md5('vubes:member:mert')::uuid,
                    md5('vubes:member:can')::uuid, md5('vubes:member:selin')::uuid)
                AND NOT sharing.revoked AND sharing.expires > CURRENT_TIMESTAMP
            """, cancellationToken);
    }
}
