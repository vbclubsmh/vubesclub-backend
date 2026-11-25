using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611250001_HostedMedia")]
public sealed class HostedMedia : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.Sql(SqlFiles.Read("HostedMedia.sql"));
    }

    protected override void Down(MigrationBuilder migration)
    {
        throw new NotSupportedException("Media references are retained when restoring an earlier application release.");
    }
}
