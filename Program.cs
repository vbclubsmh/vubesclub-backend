using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Infrastructure;

await using var app = ClubHost.Build(args);
if (args.Contains("--migrate") || args.Contains("--seed-demo"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClubDb>();
    if (args.Contains("--migrate"))
        await db.Database.MigrateAsync();
    if (args.Contains("--seed-demo"))
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Development seed only.");
        await db.Database.ExecuteSqlRawAsync(SqlFiles.Read("DevelopmentSeed.sql"));
    }
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClubDb>();
    await DevelopmentSamples.RefreshTimeline(db);
}

await app.RunAsync();

public partial class Program;
