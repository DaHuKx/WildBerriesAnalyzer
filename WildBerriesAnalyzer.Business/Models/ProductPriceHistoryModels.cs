namespace WildBerriesAnalyzer.Business.Models
{
    /// <summary>
    /// Период истории цен для графика (Mobile).
    /// </summary>
    public enum PriceHistoryPeriod
    {
        /// <summary>Последние 30 дней.</summary>
        Month = 0,

        /// <summary>Последние 182 дня (~полгода).</summary>
        HalfYear = 1,

        /// <summary>Последние 365 дней.</summary>
        Year = 2,

        /// <summary>Вся доступная история.</summary>
        AllTime = 3
    }

    public sealed class ProductPriceHistory
    {
        public int ProductId { get; init; }

        public long IdInMarket { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Brand { get; init; }

        public string? Link { get; init; }

        public string? ImageUrl { get; init; }

        public bool IsAdult { get; init; }

        public PriceHistoryPeriod Period { get; init; }

        public DateTime? PeriodFromUtc { get; init; }

        public DateTime PeriodToUtc { get; init; }

        public List<ProductPricePoint> Points { get; init; } = [];

        public ProductPriceHistorySummary Summary { get; init; } = new();
    }

    public sealed class ProductPricePoint
    {
        public decimal Price { get; init; }

        public DateTime CheckTime { get; init; }
    }

    public sealed class ProductPriceHistorySummary
    {
        public int Count { get; init; }

        public decimal? Min { get; init; }

        public decimal? Max { get; init; }

        public decimal? Median { get; init; }

        public decimal? Last { get; init; }

        public DateTime? LastCheckTime { get; init; }
    }
}
