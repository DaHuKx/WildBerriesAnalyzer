using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// Извлекает screen_name или числовой id из ссылки/строки профиля VK.
    /// </summary>
    public static partial class VkProfileLinkParser
    {
        [GeneratedRegex(
            @"^(?:https?://)?(?:m\.)?(?:vk\.com|vk\.ru|vkontakte\.ru)/(?<path>[^\s?#]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex VkUrlRegex();

        [GeneratedRegex(@"^id(?<id>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex IdPrefixRegex();

        [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
        private static partial Regex NumericIdRegex();

        public static bool TryParse(string? input, out string screenNameOrId, out bool isNumericId)
        {
            screenNameOrId = string.Empty;
            isNumericId = false;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var raw = input.Trim();
            var urlMatch = VkUrlRegex().Match(raw);
            var path = urlMatch.Success
                ? urlMatch.Groups["path"].Value.Trim('/')
                : raw.Trim().Trim('/');

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Берём первый сегмент пути (без query уже отрезано regex'ом).
            var segment = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
            if (segment.Equals("write", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith("club", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith("public", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith("event", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var idMatch = IdPrefixRegex().Match(segment);
            if (idMatch.Success)
            {
                screenNameOrId = idMatch.Groups["id"].Value;
                isNumericId = true;
                return true;
            }

            if (NumericIdRegex().IsMatch(segment))
            {
                screenNameOrId = segment;
                isNumericId = true;
                return true;
            }

            screenNameOrId = segment;
            isNumericId = false;
            return !string.IsNullOrWhiteSpace(screenNameOrId);
        }
    }
}
