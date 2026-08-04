using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Calculators
{
    public static class DiscontsCalculator
    {
        public static (decimal, WbPrice?) CalculateDiscontByAveragePrice(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var positive = pricesHistory.Where(p => p.Price > 0).ToList();
            if (positive.Count == 0)
                return (0, null);

            var avgPrice = positive.Average(p => p.Price);
            return SafeRatio(lastPrice.Price, avgPrice, CreateAggregateReference(lastPrice.ProductId, avgPrice, positive));
        }

        public static (decimal, WbPrice?) CalculateDiscontByMedianPrice(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var positive = pricesHistory.Where(p => p.Price > 0).ToList();
            if (positive.Count == 0)
                return (0, null);

            var medianPrice = GetMedianPrice(positive.Select(p => p.Price));
            return SafeRatio(lastPrice.Price, medianPrice, CreateAggregateReference(lastPrice.ProductId, medianPrice, positive));
        }

        public static (decimal, WbPrice?) CalculateDiscontByLastKnownPrice(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            WbPrice? preLastPrice;

            if (lastPrice.Id == pricesHistory[^1].Id)
            {
                preLastPrice = pricesHistory.Count > 1 ? pricesHistory[^2] : null;
            }
            else
            {
                preLastPrice = pricesHistory[^1];
            }

            if (preLastPrice is null)
                return (0, null);

            return SafeRatio(lastPrice.Price, preLastPrice.Price, preLastPrice);
        }

        public static (decimal, WbPrice?) CalculateDiscontByMinimumPrice(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var minPrice = pricesHistory.Where(p => p.Price > 0).MinBy(p => p.Price);
            if (minPrice is null)
                return (0, null);

            return SafeRatio(lastPrice.Price, minPrice.Price, minPrice);
        }

        public static (decimal, WbPrice?) CalculateDiscontByLowestPriceFor30Days(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var lastMonthTime = DateTime.UtcNow.AddDays(-30);
            WbPrice? lowestPrice = null;

            for (int i = pricesHistory.Count - 1; i >= 0; i--)
            {
                if (pricesHistory[i].CheckTime < lastMonthTime)
                    break;

                if (pricesHistory[i].Price <= 0)
                    continue;

                if (lowestPrice is null || pricesHistory[i].Price < lowestPrice.Price)
                {
                    lowestPrice = pricesHistory[i];
                }
            }

            if (lowestPrice is null)
                return (0, null);

            return SafeRatio(lastPrice.Price, lowestPrice.Price, lowestPrice);
        }

        public static (decimal, WbPrice?) CalculateDiscontByAveragePriceFor30Days(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var lastMonthTime = DateTime.UtcNow.AddDays(-30);
            var window = new List<WbPrice>();

            for (int i = pricesHistory.Count - 1; i >= 0; i--)
            {
                if (pricesHistory[i].CheckTime < lastMonthTime)
                    break;

                if (pricesHistory[i].Price <= 0)
                    continue;

                window.Add(pricesHistory[i]);
            }

            if (window.Count == 0)
                return (0, null);

            var averagePrice = window.Average(p => p.Price);
            return SafeRatio(
                lastPrice.Price,
                averagePrice,
                CreateAggregateReference(lastPrice.ProductId, averagePrice, window));
        }

        public static (decimal, WbPrice?) CalculateDiscontByMedianPriceFor30Days(WbPrice lastPrice, List<WbPrice> pricesHistory)
        {
            if (pricesHistory.Count == 0)
                return (0, null);

            var lastMonthTime = DateTime.UtcNow.AddDays(-30);
            var pricesFor30Days = new List<WbPrice>();

            for (int i = pricesHistory.Count - 1; i >= 0; i--)
            {
                if (pricesHistory[i].CheckTime < lastMonthTime)
                    break;

                if (pricesHistory[i].Price > 0)
                {
                    pricesFor30Days.Add(pricesHistory[i]);
                }
            }

            if (pricesFor30Days.Count == 0)
                return (0, null);

            var median = GetMedianPrice(pricesFor30Days.Select(p => p.Price));
            return SafeRatio(
                lastPrice.Price,
                median,
                CreateAggregateReference(lastPrice.ProductId, median, pricesFor30Days));
        }

        /// <summary>
        /// Агрегатная референсная цена (средняя/медиана).
        /// Id = -1 — маркер агрегата; CheckTime = конец периода; CreatedAt = начало периода.
        /// </summary>
        private static WbPrice CreateAggregateReference(int productId, decimal price, List<WbPrice> source)
        {
            return new WbPrice
            {
                Id = -1,
                ProductId = productId,
                Price = price,
                CheckTime = source.Max(p => p.CheckTime),
                CreatedAt = source.Min(p => p.CheckTime)
            };
        }

        private static (decimal, WbPrice?) SafeRatio(decimal currentPrice, decimal referencePrice, WbPrice? reference)
        {
            if (referencePrice <= 0 || currentPrice < 0)
                return (0, null);

            return (currentPrice / referencePrice, reference);
        }

        private static decimal GetMedianPrice(IEnumerable<decimal> pricesHistory)
        {
            var arr = pricesHistory.ToArray();
            if (arr.Length == 0) return 0;

            Array.Sort(arr);
            int mid = arr.Length / 2;

            return arr.Length % 2 == 1
                ? arr[mid]
                : (arr[mid - 1] + arr[mid]) / 2.0m;
        }
    }
}
