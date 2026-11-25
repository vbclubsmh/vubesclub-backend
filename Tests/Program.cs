using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Modules.Payments;
using VubesClub.Api.Persistence;
using VubesClub.Verification;

var postgres = Environment.GetEnvironmentVariable("VUBES_VERIFY_POSTGRES");
if (postgres != null)
{
    var settings = new NpgsqlConnectionStringBuilder(postgres);
    if (string.IsNullOrEmpty(settings.Database) || !settings.Database.EndsWith("_test", StringComparison.Ordinal))
        throw new ArgumentException("VUBES_VERIFY_POSTGRES must identify a dedicated database ending in _test.");
}

var server = new InProcessServer();
await using var app = ClubHost.Build(
    ["--ConnectionStrings:Club=" + (postgres ?? "Host=localhost;Database=vubes_verification_test")],
    builder =>
    {
        builder.Services.AddSingleton<IServer>(server);
        builder.Logging.ClearProviders();
    });

await app.StartAsync();
await using var scope = app.Services.CreateAsyncScope();
var services = scope.ServiceProvider;

using var specification = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mobile.openapi.json")));
var prefix = specification.RootElement.GetProperty("servers")[0].GetProperty("url").GetString();
var expected = specification.RootElement.GetProperty("paths").EnumerateObject()
    .SelectMany(path => path.Value.EnumerateObject().Select(method => new
    {
        Method = method.Name.ToUpperInvariant(),
        Path = prefix + path.Name,
        Authenticated = method.Value.GetProperty("security").GetArrayLength() > 0
    })).ToArray();

var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
    .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(prefix + "/", StringComparison.Ordinal) == true)
    .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method =>
        (method, Regex.Replace(endpoint.RoutePattern.RawText!, @":guid(?=})", "").TrimEnd('/')))).ToHashSet();
var requiredRoutes = expected.Select(endpoint => (endpoint.Method, endpoint.Path)).ToHashSet();
Checks.Require(endpoints.SetEquals(requiredRoutes),
    "Contract mismatch. Missing: " + string.Join(", ", requiredRoutes.Except(endpoints)) +
    " Extra: " + string.Join(", ", endpoints.Except(requiredRoutes)));
Checks.Passed($"{endpoints.Count} mobile operations match their HTTP method and route");

var protectedRoutes = expected.Where(endpoint => endpoint.Authenticated).ToArray();
foreach (var endpoint in protectedRoutes)
{
    var path = Regex.Replace(endpoint.Path, @"\{[^}]+\}", Guid.Empty.ToString());
    var reply = await server.Send(endpoint.Method, path);
    reply.Expect(HttpStatusCode.Unauthorized);
    Checks.Require(reply.Read<JsonElement>().GetProperty("code").GetString() == "SESSION_INVALID", "Unexpected access error.");
}
Checks.Passed($"{protectedRoutes.Length} protected operations reject an anonymous request in the actual middleware");
(await server.Send("GET", "/health/live")).Expect(HttpStatusCode.OK);

var gateway = services.GetRequiredService<IPaymentGateway>();
Checks.Require(!gateway.IsConfigured, "A production payment provider was unexpectedly enabled.");
try
{
    await gateway.StartAsync(new EventCharge(Guid.NewGuid(), Guid.NewGuid(), 100, "TRY", DateTime.UtcNow.AddMinutes(10)), default);
    throw new InvalidOperationException("Unconfigured gateway unexpectedly opened a payment.");
}
catch (ApiFault fault) when (fault.Status == 503 && fault.Code == "PAYMENT_PROVIDER_UNAVAILABLE") { }
(await server.Send("POST", "/integrations/payments/unconfigured/webhooks")).Expect(HttpStatusCode.NotFound);
Checks.Passed("unconfigured gateway refuses charges and rejects provider callbacks");

var db = services.GetRequiredService<ClubDb>();
var migrationSql = db.GetService<IMigrator>().GenerateScript();
var sqlTables = Regex.Matches(migrationSql, @"(?im)^CREATE TABLE ([a-z_]+) \(")
    .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
var mappedTables = db.Model.GetEntityTypes().Select(entity => entity.GetTableName()!).ToHashSet(StringComparer.Ordinal);
Checks.Require(sqlTables.SetEquals(mappedTables), "Migration tables differ from mapped entities.");
foreach (var table in mappedTables)
    Checks.Require(migrationSql.Contains($"CREATE TABLE {table} (", StringComparison.Ordinal), $"Missing table {table}.");
Checks.Passed($"Npgsql generates migration SQL for all {sqlTables.Count} mapped tables");

var catalog = services.GetRequiredService<CatalogQueries>();
var cards = catalog.Cards(catalog.PublishedVersions().OrderBy(v => v.Starts).ThenBy(v => v.Id).Take(20));
var trainers = catalog.Trainers(db.Set<Trainer>());
var eligible = catalog.EligibleEvents();
foreach (var query in new IQueryable[] { cards, trainers, eligible })
    Checks.Require(query.ToQueryString().Contains("SELECT", StringComparison.Ordinal), "SQL projection did not compile.");
Checks.Passed("catalog cards, trainers and eligibility translate to PostgreSQL SQL");

if (postgres != null)
{
    var installed = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet();
    Checks.Require(installed.SetEquals(db.Database.GetMigrations()), "Test DB migrations differ from the release.");
    _ = await cards.ToListAsync();
    _ = await trainers.Take(1).ToListAsync();
    _ = await eligible.Take(1).ToListAsync();
    Checks.Passed("existing PostgreSQL test database migrations and read projections");
    await PostgreSqlAcceptance.Run(postgres);
}
else
{
    Console.WriteLine("SKIP  live PostgreSQL (set VUBES_VERIFY_POSTGRES to a migrated _test database)");
}

await app.StopAsync();
