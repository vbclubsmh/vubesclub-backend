using System.Globalization;

namespace VubesClub.Api.Infrastructure;

public readonly record struct Paging(int Skip, int Limit)
{
    public static Paging Parse(string? cursor, int? limit)
    {
        var skip = 0;
        if (limit is < 1 or > 100 || cursor != null &&
            (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out skip) || skip < 0))
            throw new ApiFault(400, "INVALID_PAGE", "Sayfalama değerleri geçersiz.");

        return new Paging(skip, limit ?? 20);
    }

    public string? Next(int count) => count > Limit ? ((long)Skip + Limit).ToString(CultureInfo.InvariantCulture) : null;
}
