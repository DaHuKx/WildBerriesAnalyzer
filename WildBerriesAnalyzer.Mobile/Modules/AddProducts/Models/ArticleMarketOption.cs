using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Modules.AddProducts.Models
{
    public sealed class ArticleMarketOption
    {
        public ArticleMarketOption(MarketType marketType, string title)
        {
            MarketType = marketType;
            Title = title;
        }

        public MarketType MarketType { get; }

        public string Title { get; }

        public override string ToString() => Title;
    }
}
