using System.Net;

namespace VubesClub.Api.Modules.Identity;

/// <summary>Giriş kodu e-postası: Afterglow siyah/krem teması, Türkçe.</summary>
public static class OtpEmail
{
    public static string Subject(string code) => $"Vubes Club giriş kodun: {code}";

    public static string Text(string? name, string code, int minutes) =>
        $"""
        {Greeting(name)}

        Vubes Club giriş kodun: {code}

        Bu kod {minutes} dakika geçerli. Kodu kimseyle paylaşma.
        Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin.

        Vubes Club · More than fitness.
        """;

    public static string Html(string? name, string code, int minutes, string logoUrl)
    {
        // Rakamlar görsel: Gmail (iOS) açık renkli metni koyu modda koyulaştırıyor, görselleri ise değiştirmiyor.
        var assets = logoUrl[..(logoUrl.LastIndexOf('/') + 1)] + "mail/";
        var digits = string.Concat(code.Where(char.IsAsciiDigit).Select(c =>
            $"<td style=\"padding:0 3px;\"><img src=\"{WebUtility.HtmlEncode(assets)}d{c}.png\" width=\"40\" height=\"58\" alt=\"{c}\" " +
            $"style=\"display:block;border:0;width:40px;height:58px;color:#a39e90;font:500 38px 'Courier New',Courier,monospace;\"></td>"));
        // Her koyu alan hem bgcolor hem arka plan görseliyle verilir: Gmail/Outlook koyu modda bunları ters çevirmez.
        static string Dark(string hex) => $"bgcolor=\"{hex}\" style=\"background-color:{hex};background-image:linear-gradient({hex},{hex});";
        var page = Dark("#222522");
        var card = Dark("#2d322e");
        return $$"""
        <!doctype html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="dark light">
        <meta name="supported-color-schemes" content="dark light">
        <title>Vubes Club giriş kodu</title>
        </head>
        <body style="margin:0;padding:0;background:#222522;">
        <div style="display:none;max-height:0;overflow:hidden;opacity:0;">Giriş kodun {{code}}. {{minutes}} dakika geçerli.</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" {{page}}">
          <tr><td align="center" {{page}}padding:36px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;">
              <tr><td align="center" {{page}}padding-bottom:28px;">
                <img src="{{WebUtility.HtmlEncode(logoUrl)}}" width="130" alt="Vubes Club"
                     style="display:block;border:0;width:130px;height:auto;color:#ffffff;font:600 18px Helvetica,Arial,sans-serif;letter-spacing:4px;">
              </td></tr>
              <tr><td {{card}}border:1px solid #454c44;border-radius:16px;padding:36px 28px;">
                <p style="margin:0 0 6px;font:300 28px 'Helvetica Neue',Helvetica,Arial,sans-serif;color:#a39e90;letter-spacing:-0.5px;">{{WebUtility.HtmlEncode(Greeting(name))}}</p>
                <p style="margin:0 0 26px;font:400 15px/1.55 Helvetica,Arial,sans-serif;color:#9a9486;">Vubes Club'a giriş için tek kullanımlık kodun aşağıda.</p>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                  <td align="center" {{page}}border:1px solid #454c44;border-radius:12px;padding:22px 8px;">
                    <table role="presentation" align="center" cellpadding="0" cellspacing="0"><tr>{{digits}}</tr></table>
                  </td></tr></table>
                <p style="margin:24px 0 0;font:400 14px/1.55 Helvetica,Arial,sans-serif;color:#9a9486;">Bu kod <strong style="color:#a39e90;">{{minutes}} dakika</strong> geçerli. Uygulamaya dönüp kodu gir.</p>
                <p style="margin:14px 0 0;font:400 13px/1.55 Helvetica,Arial,sans-serif;color:#9a9486;">Kodu kimseyle paylaşma. Vubes Club ekibi seni asla kodunu paylaşman için aramaz.</p>
              </td></tr>
              <tr><td align="center" {{page}}padding:22px 12px 0;font:400 12px/1.6 Helvetica,Arial,sans-serif;color:#8d8778;">
                Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin.<br>
                Vubes Club · More than fitness.
              </td></tr>
            </table>
          </td></tr>
        </table>
        </body>
        </html>
        """;
    }

    private static string Greeting(string? name)
    {
        var first = name?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Merhaba" : $"Merhaba {first}";
    }
}
