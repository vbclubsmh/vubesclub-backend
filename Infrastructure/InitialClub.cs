using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611220001_InitialClub")]
public sealed class InitialClub : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.Sql(SqlFiles.Read("Schema.sql"));
    }

    protected override void Down(MigrationBuilder migration)
    {
        throw new NotSupportedException("Restore a backup to undo the initial database creation.");
    }
}
