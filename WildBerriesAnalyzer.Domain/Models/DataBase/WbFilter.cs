using System.Collections.Generic;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class WbFilter : BaseDbEntity
    {
        public int UserId { get; set; }
        public int DiscontMinPercent { get; set; } = 1;
        public int MinReviewsCount { get; set; } = 0;
        public float MinRating { get; set; } = 0;
        public ProductsFilterType ProductsFilterType { get; set; }

        /// <summary>
        /// null или пустой список = все стратегии базовой цены.
        /// </summary>
        public List<ReferencePriceStrategy>? ReferencePriceStrartegies { get; set; }

        /// <summary>
        /// Маркетплейсы, по которым учитывать скидки.
        /// null или пустой список = все магазины.
        /// </summary>
        public List<MarketType>? MarketTypes { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public WbUser? User { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public List<WbFilterCategory>? FilterCategories { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public List<WbFilterBag>? BagProducts { get; set; }

        /// <summary>
        /// Для товаров из корзины пользователя учитывается только мин. скидка;
        /// отзывы и рейтинг не проверяются.
        /// </summary>
        public bool FilterApprovedForDiscont(Discont discont, bool isInUserBag = false)
        {
            if (discont?.Product is null)
            {
                return false;
            }

            if (MarketTypes is { Count: > 0 }
                && !MarketTypes.Contains(discont.Product.MarketType))
            {
                return false;
            }

            if (DiscontMinPercent > discont.DiscontPercent)
            {
                return false;
            }

            if (isInUserBag)
            {
                return true;
            }

            return MinReviewsCount <= discont.Product.FeedBacksCount
                   && MinRating <= discont.Product.ReviewRating;
        }
    }
}
