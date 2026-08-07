using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Server.Services
{
    /// <summary>
    /// SemVer из трёх тактов: major.minor.patch (например 1.0.19).
    /// </summary>
    public static class ClientSemVersion
    {
        private static readonly Regex Pattern = new(
            @"^\d+\.\d+\.\d+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsValid(string? value) =>
            !string.IsNullOrWhiteSpace(value) && Pattern.IsMatch(value.Trim());

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return IsValid(trimmed) ? trimmed : null;
        }

        public static bool TryCompare(string? left, string? right, out int comparison)
        {
            comparison = 0;
            if (!TryParse(left, out var a) || !TryParse(right, out var b))
            {
                return false;
            }

            comparison = a.CompareTo(b);
            return true;
        }

        public static bool IsOlderThan(string? client, string? latest) =>
            TryCompare(client, latest, out var cmp) && cmp < 0;

        private static bool TryParse(string? value, out Version version)
        {
            version = new Version(0, 0, 0);
            var normalized = Normalize(value);
            return normalized is not null && Version.TryParse(normalized, out version);
        }
    }
}
