using System.Net;

namespace VubesClub.Api.Modules.Booking;

/// <summary>"Kaydınız alınmıştır" e-postası: Afterglow siyah/krem teması, QR kodu satır içi görsel olarak taşır.</summary>
public static class BookingEmail
{
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
    private static readonly string[] Days =
        ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];

    public static string When(DateTime startsUtc, string timeZone)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); } catch { zone = TimeZoneInfo.Utc; }
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startsUtc, DateTimeKind.Utc), zone);
        return $"{local.Day} {Months[local.Month - 1]} {local.Year}, {Days[(int)local.DayOfWeek]} · {local:HH:mm}";
    }

    public static string Subject(string title) => $"Kaydınız alındı · {title}";

    public static string Text(string? name, string title, string when, string place, string qr) =>
        $"""
        {Greeting(name)}

        Kaydınız alınmıştır.

        {title}
        {when}
        {place}

        Etkinlik girişinde standımızda bu kodu okutun: {qr}
        (QR kodu e-postadaki görselde ve uygulamada Etkinliklerim bölümünde de var.)

        Vubes Club · More than fitness.
        """;

    public static string Html(string? name, string title, string when, string place, string logoUrl, string qrCid)
    {
        static string Dark(string hex) => $"bgcolor=\"{hex}\" style=\"background-color:{hex};background-image:linear-gradient({hex},{hex});";
        static string E(string value) => WebUtility.HtmlEncode(value);
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
        <title>Kaydınız alındı</title>
        </head>
        <body style="margin:0;padding:0;background:#222522;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" {{page}}">
          <tr><td align="center" {{page}}padding:36px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;">
              <tr><td align="center" {{page}}padding-bottom:28px;">
                <img src="{{E(logoUrl)}}" width="130" alt="Vubes Club"
                     style="display:block;border:0;width:130px;height:auto;color:#ffffff;font:600 18px Helvetica,Arial,sans-serif;letter-spacing:4px;">
              </td></tr>
              <tr><td {{card}}border:1px solid #454c44;border-radius:16px;padding:36px 28px;">
                <p style="margin:0 0 6px;font:300 28px 'Helvetica Neue',Helvetica,Arial,sans-serif;color:#a39e90;letter-spacing:-0.5px;">{{E(Greeting(name))}}</p>
                <p style="margin:0 0 22px;font:400 17px/1.5 Helvetica,Arial,sans-serif;color:#d9d3c3;">Kaydınız alınmıştır.</p>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                  <td {{page}}border:1px solid #454c44;border-radius:12px;padding:18px 18px;">
                    <p style="margin:0 0 6px;font:300 24px 'Helvetica Neue',Helvetica,Arial,sans-serif;color:#d9d3c3;letter-spacing:-0.3px;">{{E(title)}}</p>
                    <p style="margin:0;font:400 14px/1.6 Helvetica,Arial,sans-serif;color:#9a9486;">{{E(when)}}<br>{{E(place)}}</p>
                  </td></tr></table>
                <p style="margin:26px 0 14px;text-align:center;font:400 14px/1.55 Helvetica,Arial,sans-serif;color:#9a9486;">Etkinlik girişinde standımızda bu QR kodu okutun.</p>
                <table role="presentation" align="center" cellpadding="0" cellspacing="0"><tr>
                  <td bgcolor="#ffffff" style="background-color:#ffffff;border-radius:14px;padding:10px;">
                    <img src="cid:{{qrCid}}" width="220" height="220" alt="Etkinlik QR kodu"
                         style="display:block;border:0;width:220px;height:220px;">
                  </td></tr></table>
                <p style="margin:18px 0 0;text-align:center;font:400 13px/1.55 Helvetica,Arial,sans-serif;color:#9a9486;">QR kodu uygulamada <strong style="color:#a39e90;">Etkinliklerim</strong> bölümünde de bulabilirsiniz.</p>
              </td></tr>
              <tr><td align="center" {{page}}padding:22px 12px 0;font:400 12px/1.6 Helvetica,Arial,sans-serif;color:#8d8778;">
                Kodu başkalarıyla paylaşmayın.<br>
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
