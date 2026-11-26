using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611260002_ApplicationBody")]
public sealed class ApplicationBody : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE otp_challenge
            ADD COLUMN IF NOT EXISTS height_cm integer,
            ADD COLUMN IF NOT EXISTS weight_kg integer;
        ALTER TABLE application_entry
            ADD COLUMN IF NOT EXISTS height_cm integer,
            ADD COLUMN IF NOT EXISTS weight_kg integer;
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
