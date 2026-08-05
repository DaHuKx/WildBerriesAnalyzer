using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Business.Helpers
{
    public static class ProductHelper
    {
        private static readonly Regex CatalogUrlRegex = new(
            @"https?://(?:www\.)?wildberries\.ru/catalog/(\d+)(?:/[^\s]*)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CatalogPathRegex = new(
            @"(?:www\.)?wildberries\.ru/catalog/(\d+)(?:/[^\s]*)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ExtractCleanArticle(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // 1. Если строка уже состоит только из цифр, возвращаем её как есть
            if (Regex.IsMatch(input, @"^\d+$"))
            {
                return input;
            }

            // 2. Если это ссылка, извлекаем группу цифр, идущих сразу после /catalog/
            var match = Regex.Match(input, @"wildberries\.ru/catalog/(\d+)", RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : input;
        }

        /// <summary>
        /// Достаёт артикул или канонический URL WB из текста «Поделиться»
        /// (часто это название + ссылка в одной строке).
        /// </summary>
        public static bool TryExtractArticleInput(string? sharedText, out string articleOrUrl)
        {
            articleOrUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(sharedText))
            {
                return false;
            }

            var text = sharedText.Trim();

            var urlMatch = CatalogUrlRegex.Match(text);
            if (urlMatch.Success)
            {
                articleOrUrl = $"https://www.wildberries.ru/catalog/{urlMatch.Groups[1].Value}";
                return true;
            }

            var pathMatch = CatalogPathRegex.Match(text);
            if (pathMatch.Success)
            {
                articleOrUrl = $"https://www.wildberries.ru/catalog/{pathMatch.Groups[1].Value}";
                return true;
            }

            if (Regex.IsMatch(text, @"^\d{5,10}$") && text[0] != '0')
            {
                articleOrUrl = text;
                return true;
            }

            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Regex.IsMatch(line, @"^\d{5,10}$") && line[0] != '0')
                {
                    articleOrUrl = line;
                    return true;
                }
            }

            return false;
        }
    }
}
