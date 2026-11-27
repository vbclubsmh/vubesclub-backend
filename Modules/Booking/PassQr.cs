using QRCoder;

namespace VubesClub.Api.Modules.Booking;

/// <summary>Katılım kartının QR içeriği: kişisel veri taşımayan, sunucuda doğrulanan opak değer.</summary>
public static class PassQr
{
    private const string Prefix = "VUBES-";

    public static string Payload(string nonce) => Prefix + nonce;

    public static string? Nonce(string? payload)
    {
        var text = payload?.Trim();
        return text is { Length: > 6 and <= 200 } && text.StartsWith(Prefix, StringComparison.Ordinal)
            ? text[Prefix.Length..] : null;
    }

    /// <summary>Beyaz zeminli, geniş sessiz alanlı PNG; e-postada ve ekranda okunaklıdır.</summary>
    public static byte[] Png(string payload, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule, [0, 0, 0, 255], [255, 255, 255, 255], true);
    }
}
