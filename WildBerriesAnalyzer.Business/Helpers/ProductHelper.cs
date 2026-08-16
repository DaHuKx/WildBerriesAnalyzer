using System.Text.RegularExpressions;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Business.Helpers
{
    public static class ProductHelper
    {
        private const int WbMinLength = 5;
        private const int WbMaxLength = 10;
        private const int OzonMinLength = 6;
        private const int OzonMaxLength = 16;

        private static readonly Regex CatalogUrlRegex = new(
            @"https?://(?:www\.)?wildberries\.ru/catalog/(\d+)(?:/[^\s]*)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CatalogPathRegex = new(
            @"(?:www\.)?wildberries\.ru/catalog/(\d+)(?:/[^\s]*)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BasketShareIdRegex = new(
            @"(?:https?://)?(?:www\.)?wildberries\.ru/(?:lk/)?basket\?(?:[^#\s]*&)?shareId=([A-Za-z0-9_-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Общая корзина Ozon: https://www.ozon.ru/cart?share=7GIHyzu
        /// </summary>
        private static readonly Regex OzonCartShareIdRegex = new(
            @"(?:https?://)?(?:[\w-]+\.)?ozon\.ru/cart\?(?:[^#\s]*&)?share=([A-Za-z0-9_-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WbCatalogIdRegex = new(
            @"wildberries\.ru/catalog/(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex OzonProductSlugRegex = new(
            @"(?:https?://)?(?:[\w-]+\.)?ozon\.ru/product/([^/?#\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex OzonDetailIdRegex = new(
            @"(?:https?://)?(?:[\w-]+\.)?ozon\.ru/(?:context/)?detail/id/(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Короткие ссылки вида https://ozon.ru/t/RhWvoBC
        /// </summary>
        private static readonly Regex OzonShortLinkRegex = new(
            @"(?:https?://)?(?:[\w-]+\.)?ozon\.ru/t/([A-Za-z0-9_-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TrailingDigitsRegex = new(
            @"(\d+)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Извлекает shareId из ссылки на общую корзину WB.
        /// Пример: https://wildberries.ru/basket?shareId=6byrsfmed6
        /// </summary>
        public static bool TryExtractBasketShareId(string? text, out string shareId)
        {
            shareId = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = BasketShareIdRegex.Match(text.Trim());
            if (!match.Success)
            {
                return false;
            }

            shareId = match.Groups[1].Value;
            return shareId.Length > 0;
        }

        /// <summary>
        /// Извлекает share-токен из ссылки на общую корзину Ozon.
        /// Пример: https://www.ozon.ru/cart?share=7GIHyzu
        /// </summary>
        public static bool TryExtractOzonCartShareId(string? text, out string shareToken)
        {
            shareToken = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = OzonCartShareIdRegex.Match(text.Trim());
            if (!match.Success)
            {
                return false;
            }

            shareToken = match.Groups[1].Value;
            return shareToken.Length > 0;
        }

        public static string ExtractCleanArticle(string input) =>
            ExtractCleanArticle(input, MarketType.Wildberries);

        public static string ExtractCleanArticle(string input, MarketType marketType)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            var text = input.Trim();
            if (marketType == MarketType.Ozon && TryExtractOzonShortLink(text, out var shortCode))
            {
                return $"https://www.ozon.ru/t/{shortCode}";
            }

            return TryExtractArticle(text, marketType, out var article)
                ? article
                : text;
        }

        /// <summary>
        /// Нормализованный вход Ozon для скрапинга: числовой SKU или URL (/product/… или /t/…).
        /// </summary>
        public static bool TryNormalizeOzonProductRef(string? input, out string productRef)
        {
            productRef = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var text = input.Trim();
            if (TryExtractOzonShortLink(text, out var shortCode))
            {
                productRef = $"https://www.ozon.ru/t/{shortCode}";
                return true;
            }

            if (TryExtractOzonArticle(text, out var article) && IsValidOzonArticle(article))
            {
                productRef = article;
                return true;
            }

            // Уже нормализованный URL /product/…
            var productMatch = OzonProductSlugRegex.Match(text);
            if (productMatch.Success)
            {
                var slug = productMatch.Groups[1].Value.TrimEnd('/');
                productRef = $"https://www.ozon.ru/product/{slug}/";
                return true;
            }

            return false;
        }

        public static bool IsOzonProductUrl(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (OzonShortLinkRegex.IsMatch(value) ||
             OzonProductSlugRegex.IsMatch(value) ||
             OzonDetailIdRegex.IsMatch(value));

        public static bool TryExtractArticle(string? input, MarketType marketType, out string article)
        {
            article = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var text = input.Trim();
            return marketType switch
            {
                MarketType.Ozon => TryExtractOzonArticle(text, out article),
                _ => TryExtractWbArticle(text, out article)
            };
        }

        public static bool IsValidArticleOrUrl(string? input, MarketType marketType)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var text = input.Trim();
            if (marketType == MarketType.Ozon && TryNormalizeOzonProductRef(text, out _))
            {
                return true;
            }

            if (!TryExtractArticle(text, marketType, out var article))
            {
                return false;
            }

            return marketType switch
            {
                MarketType.Ozon => IsValidOzonArticle(article),
                _ => IsValidWbArticle(article)
            };
        }

        public static string GetArticleValidationError(string? input, MarketType marketType)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "Артикул не может быть пустым.";
            }

            var text = input.Trim();
            var isUrl = Regex.IsMatch(text, @"^https?://", RegexOptions.IgnoreCase);

            if (marketType == MarketType.Ozon)
            {
                if (TryExtractOzonShortLink(text, out _))
                {
                    return "Некорректная короткая ссылка Ozon.";
                }

                if (TryExtractOzonArticle(text, out var ozonId))
                {
                    if (!IsValidOzonArticle(ozonId))
                    {
                        return $"Артикул Ozon должен содержать от {OzonMinLength} до {OzonMaxLength} цифр.";
                    }

                    return "Некорректный артикул Ozon.";
                }

                if (isUrl)
                {
                    return "Ссылка должна быть на товар Ozon (например, https://www.ozon.ru/product/1678901234/ или https://ozon.ru/t/RhWvoBC).";
                }

                return $"Некорректный формат. Ожидается артикул Ozon ({OzonMinLength}-{OzonMaxLength} цифр) или ссылка на товар Ozon.";
            }

            if (TryExtractWbArticle(text, out var wbId))
            {
                if (wbId.Length < WbMinLength || wbId.Length > WbMaxLength)
                {
                    return $"Артикул должен содержать от {WbMinLength} до {WbMaxLength} цифр.";
                }

                if (wbId[0] == '0')
                {
                    return "Артикул не может начинаться с нуля.";
                }

                return "Некорректный артикул Wildberries.";
            }

            if (isUrl)
            {
                return "Ссылка должна быть корректным URL каталога Wildberries (например, https://www.wildberries.ru/catalog/567263073).";
            }

            return "Некорректный формат. Ожидается артикул (5-10 цифр без лидирующего нуля) или ссылка на товар Wildberries.";
        }

        /// <summary>
        /// Достаёт артикул или канонический URL WB/Ozon из текста «Поделиться»
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

            if (TryExtractOzonProductShareInput(text, out articleOrUrl))
            {
                return true;
            }

            if (Regex.IsMatch(text, @"^\d{5,10}$") && text[0] != '0')
            {
                articleOrUrl = text;
                return true;
            }

            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryExtractOzonProductShareInput(line, out articleOrUrl))
                {
                    return true;
                }

                if (Regex.IsMatch(line, @"^\d{5,10}$") && line[0] != '0')
                {
                    articleOrUrl = line;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ссылка на карточку Ozon в тексте «Поделиться» (не голый SKU и не /cart?share=).
        /// </summary>
        public static bool LooksLikeOzonProductInput(string? text) =>
            TryExtractOzonProductShareInput(text, out _);

        private static bool TryExtractOzonProductShareInput(string? text, out string articleOrUrl)
        {
            articleOrUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(text) || TryExtractOzonCartShareId(text, out _))
            {
                return false;
            }

            if (TryExtractOzonShortLink(text, out var shortCode))
            {
                articleOrUrl = $"https://www.ozon.ru/t/{shortCode}";
                return true;
            }

            if (TryExtractOzonArticle(text, out var article) &&
                IsValidOzonArticle(article) &&
                (OzonProductSlugRegex.IsMatch(text) || OzonDetailIdRegex.IsMatch(text)))
            {
                articleOrUrl = article;
                return true;
            }

            var productMatch = OzonProductSlugRegex.Match(text);
            if (productMatch.Success)
            {
                var slug = productMatch.Groups[1].Value.TrimEnd('/');
                articleOrUrl = $"https://www.ozon.ru/product/{slug}/";
                return true;
            }

            return false;
        }

        private static bool TryExtractWbArticle(string text, out string article)
        {
            article = string.Empty;

            if (Regex.IsMatch(text, @"^\d+$"))
            {
                article = text;
                return true;
            }

            var match = WbCatalogIdRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            article = match.Groups[1].Value;
            return true;
        }

        private static bool TryExtractOzonArticle(string text, out string article)
        {
            article = string.Empty;

            // Короткие ссылки не содержат SKU — их резолвит скрапер.
            if (TryExtractOzonShortLink(text, out _))
            {
                return false;
            }

            if (Regex.IsMatch(text, @"^\d+$"))
            {
                article = text;
                return true;
            }

            var productMatch = OzonProductSlugRegex.Match(text);
            if (productMatch.Success)
            {
                var slug = productMatch.Groups[1].Value.TrimEnd('/');
                var digits = TrailingDigitsRegex.Match(slug);
                if (digits.Success)
                {
                    article = digits.Groups[1].Value;
                    return true;
                }
            }

            var detailMatch = OzonDetailIdRegex.Match(text);
            if (!detailMatch.Success)
            {
                return false;
            }

            article = detailMatch.Groups[1].Value;
            return true;
        }

        private static bool TryExtractOzonShortLink(string text, out string code)
        {
            code = string.Empty;
            var match = OzonShortLinkRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            code = match.Groups[1].Value;
            return code.Length > 0;
        }

        private static bool IsValidWbArticle(string article) =>
            article.Length >= WbMinLength &&
            article.Length <= WbMaxLength &&
            article[0] != '0' &&
            Regex.IsMatch(article, @"^\d+$");

        private static bool IsValidOzonArticle(string article) =>
            article.Length >= OzonMinLength &&
            article.Length <= OzonMaxLength &&
            Regex.IsMatch(article, @"^\d+$");
    }
}
