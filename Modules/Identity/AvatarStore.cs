using Microsoft.Extensions.FileProviders;

namespace VubesClub.Api.Modules.Identity;

/// <summary>Üye profil fotoğraflarını diskte saklar; yalnızca doğrulanmış JPEG/PNG kabul eder.</summary>
public sealed class AvatarStore
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private const string UrlPrefix = "/media/members/";

    public AvatarStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Root = configuration["Media:AvatarRoot"]
            ?? Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "media", "members");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public IFileProvider Files => new PhysicalFileProvider(Root);

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PngEnd = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

    /// <summary>Başlığı ve bitişi tutarlı JPEG/PNG ise uzantısını döndürür; kesik ya da başka türdeki dosyalar null.</summary>
    public static string? ExtensionOf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32) return null;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            var end = bytes.Length;
            while (end > 2 && bytes[end - 1] == 0) end--;
            return bytes[end - 2] == 0xFF && bytes[end - 1] == 0xD9 ? ".jpg" : null;
        }

        return bytes[..8].SequenceEqual(PngSignature) && bytes[^8..].SequenceEqual(PngEnd) ? ".png" : null;
    }

    public async Task<string> SaveAsync(Guid memberId, byte[] bytes, string extension, CancellationToken ct)
    {
        var name = $"{memberId:N}-{Guid.NewGuid().ToString("N")[..10]}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(Root, name), bytes, ct);
        return UrlPrefix + name;
    }

    /// <summary>Üyenin, <paramref name="keep"/> dışındaki tüm eski fotoğraflarını siler (eşzamanlı yüklemelerde yetim kalmaz).</summary>
    public void RemoveOthers(Guid memberId, string? keep)
    {
        var keepName = keep is null ? null : Path.GetFileName(keep);
        foreach (var file in Directory.EnumerateFiles(Root, $"{memberId:N}-*"))
        {
            if (Path.GetFileName(file) == keepName) continue;
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    /// <summary>Yalnızca bu klasördeki üye fotoğraflarını siler.</summary>
    public void Remove(string? url)
    {
        if (url is null || !url.StartsWith(UrlPrefix, StringComparison.Ordinal)) return;
        var name = Path.GetFileName(url);
        if (name.Length == 0 || name != url[UrlPrefix.Length..]) return;
        try { File.Delete(Path.Combine(Root, name)); } catch (IOException) { }
    }
}
