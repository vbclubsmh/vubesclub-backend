using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

internal static class SqlFiles
{
    public static string Read(string name)
    {
        var assembly = typeof(ClubDb).Assembly;
        using var stream = assembly.GetManifestResourceStream($"VubesClub.Api.Database.{name}")
            ?? throw new InvalidOperationException($"Missing database resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
