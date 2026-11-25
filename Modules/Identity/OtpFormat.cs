namespace VubesClub.Api.Modules.Identity;

internal static class OtpFormat
{
    public const int Length = 4;

    public static bool IsValid(string? value) => value?.Length == Length && value.All(char.IsAsciiDigit);
}
