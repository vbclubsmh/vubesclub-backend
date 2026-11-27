using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611280002_EventFeatured")]
public sealed class EventFeatured : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE club_event ADD COLUMN IF NOT EXISTS featured boolean NOT NULL DEFAULT false;
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
