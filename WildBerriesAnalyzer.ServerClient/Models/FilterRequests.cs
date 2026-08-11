using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.ServerClient.Models
{
    public class UpdateFilterRequest
    {
        public int DiscontMinPercent { get; set; }

        public int MinReviewsCount { get; set; }

        public float MinRating { get; set; }

        public ProductsFilterType ProductsFilterType { get; set; }

        /// <summary>
        /// null или пустой список = все стратегии.
        /// </summary>
        public List<ReferencePriceStrategy>? ReferencePriceStrartegies { get; set; }

        /// <summary>
        /// null или пустой список = все магазины.
        /// </summary>
        public List<MarketType>? MarketTypes { get; set; }
    }

    public class AddBagProductsRequest
    {
        public List<string> Articles { get; set; } = [];
    }

    public class AddBagFromBasketShareRequest
    {
        public string ShareUrl { get; set; } = string.Empty;
    }

    public class RemoveBagProductsRequest
    {
        public List<int> ProductIds { get; set; } = [];
    }

    public class AddFilterCategoryRequest
    {
        public int CategoryId { get; set; }

        public CategoryFilterType Type { get; set; }
    }
}
