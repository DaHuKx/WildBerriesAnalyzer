using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Business.Helpers
{
    public static class ProductHelper
    {
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
    }
}
