using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Models
{
    public sealed class ParseProductsPricesResult
    {
        public bool Success { get; init; } = true;

        /// <summary>
        /// Ошибка авторизации / пустой ответ, похожий на протухший token/cookie.
        /// </summary>
        public bool IsAuthFailure { get; init; }

        /// <summary>
        /// Временный сетевой сбой (DNS, IPv6 unreachable и т.п.) — не про token/cookie.
        /// </summary>
        public bool IsNetworkFailure { get; init; }

        /// <summary>
        /// HTTP-код ответа маркетплейса (например 498 у WB), если известен.
        /// </summary>
        public int? HttpStatusCode { get; init; }

        public string? ErrorMessage { get; init; }

        public IReadOnlyList<WbPrice> Prices { get; init; } = Array.Empty<WbPrice>();

        /// <summary>
        /// Товары, для которых из ответа WB обновлены Rating / ReviewRating / FeedBacksCount / IsAdult.
        /// </summary>
        public IReadOnlyList<WbProduct> ProductsWithRefreshedMeta { get; init; } = Array.Empty<WbProduct>();

        public static ParseProductsPricesResult Failed(
            string errorMessage,
            bool isAuthFailure = false,
            bool isNetworkFailure = false,
            int? httpStatusCode = null) =>
            new()
            {
                Success = false,
                IsAuthFailure = isAuthFailure,
                IsNetworkFailure = isNetworkFailure,
                HttpStatusCode = httpStatusCode,
                ErrorMessage = errorMessage
            };
    }
}
