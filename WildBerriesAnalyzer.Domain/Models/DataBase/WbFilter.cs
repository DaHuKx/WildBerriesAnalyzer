using Newtonsoft.Json;
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

        [JsonIgnore]
        public WbUser User { get; set; }

        [JsonIgnore]
        public List<ReferencePriceStrategy>? ReferencePriceStrartegies { get; set; }

        [JsonIgnore]
        public List<WbFilterCategory>? FilterCategories { get; set; }

        [JsonIgnore]
        public List<WbFilterBag>? BagProducts { get; set; }

        public bool FilterApprovedForDiscont(Discont discont)
        {
            if (!(DiscontMinPercent <= discont.DiscontPercent &&
                  MinReviewsCount <= discont.Product.FeedBacksCount &&
                  MinRating <= discont.Product.ReviewRating))
            {
                return false;
            }

            return true;
        }
    }
}
