using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611280003_FieldCheckIn")]
public sealed class FieldCheckIn : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE booking_row ADD COLUMN IF NOT EXISTS checked_in_at timestamptz;
        ALTER TABLE booking_row ADD COLUMN IF NOT EXISTS confirmation_sent_at timestamptz;
        ALTER TABLE member ADD COLUMN IF NOT EXISTS field_staff boolean NOT NULL DEFAULT false;
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
