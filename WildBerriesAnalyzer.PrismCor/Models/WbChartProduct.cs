using LiveCharts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.PrismCore.Models
{
    public class WbChartProduct : WbProduct
    {
        public WbChartProduct(WbProduct product)
        {
            Brand = product.Brand;
            Id = product.Id;
            IdInMarket = product.IdInMarket;
            Name = product.Name;
            Link = product.Link;
            FeedBacksCount = product.FeedBacksCount;
            PriceFromInit = product.PriceFromInit;
            Rating = product.Rating;
            PricesHistory = product.PricesHistory;
            ReviewRating = product.ReviewRating;
            ImageUrl = product.ImageUrl;
        }

        public List<string> ChartLabels { get; set; }
        public SeriesCollection Chart { get; set; }

        public static ObservableCollection<WbChartProduct> ConvertToWbChartProducts(IEnumerable<WbProduct> wbProducts)
        {
            if (wbProducts == null)
            {
                return new ObservableCollection<WbChartProduct>();
            }

            return new ObservableCollection<WbChartProduct>(wbProducts.Select(wbproduct => new WbChartProduct(wbproduct)));
        }
    }
}
