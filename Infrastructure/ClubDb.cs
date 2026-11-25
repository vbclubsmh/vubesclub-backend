using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

public sealed class ClubDb(DbContextOptions<ClubDb> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        foreach (var type in typeof(ClubDb).Assembly.GetTypes()
                     .Where(type => type.Namespace == "VubesClub.Api.Persistence"))
        {
            var entity = builder.Entity(type);
            entity.HasKey("Id");
            entity.ToTable(JsonNamingPolicy.SnakeCaseLower.ConvertName(type.Name));

            foreach (var property in entity.Metadata.GetProperties())
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
        }
    }

    public async Task LockAsync(string resource, CancellationToken ct)
    {
        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
    }
}
