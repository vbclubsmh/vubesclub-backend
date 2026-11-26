using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611260004_TrainerActive")]
public sealed class TrainerActive : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE trainer ADD COLUMN IF NOT EXISTS active boolean NOT NULL DEFAULT true;
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
