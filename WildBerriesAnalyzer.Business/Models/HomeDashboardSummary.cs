namespace WildBerriesAnalyzer.Business.Models
{
    public sealed class HomeDashboardSummary
    {
        public long ProductsCount { get; set; }

        public long UserDiscountsCount { get; set; }

        public long AllDiscountsCount { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public DateTime? NextUpdateAt { get; set; }

        public bool UpdatesEnabled { get; set; }
    }
}
