using WildBerriesAnalyzer.Business.Calculators;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    public class DiscontsService : IDiscontsService
    {
        private readonly Dictionary<ReferencePriceStrategy, Func<WbPrice, List<WbPrice>, (decimal, WbPrice?)>> _strategies;

        public DiscontsService()
        {
            _strategies = new Dictionary<ReferencePriceStrategy, Func<WbPrice, List<WbPrice>, (decimal, WbPrice?)>>
            {
                [ReferencePriceStrategy.LastKnownPrice] = DiscontsCalculator.CalculateDiscontByLastKnownPrice,
                [ReferencePriceStrategy.AveragePrice] = DiscontsCalculator.CalculateDiscontByAveragePrice,
                [ReferencePriceStrategy.MinimumHistorical] = DiscontsCalculator.CalculateDiscontByMinimumPrice,
                [ReferencePriceStrategy.Median] = DiscontsCalculator.CalculateDiscontByMedianPrice,
                [ReferencePriceStrategy.LowestPriceForLast30Days] = DiscontsCalculator.CalculateDiscontByLowestPriceFor30Days,
                [ReferencePriceStrategy.AveragePriceForLast30Days] = DiscontsCalculator.CalculateDiscontByAveragePriceFor30Days,
                [ReferencePriceStrategy.MedianPriceForLast30Days] = DiscontsCalculator.CalculateDiscontByMedianPriceFor30Days
            };
        }

        public List<Discont> GetDiscontsFromProducts(IEnumerable<WbProduct> products, ReferencePriceStrategy strategy)
        {
            var calculateFunc = _strategies[strategy];
            var productsList = products as IList<WbProduct> ?? products.ToList();

            var result = productsList.AsParallel()
                                     .Where(product => product.PricesHistory is { Count: > 0 })
                                     .Select(product =>
                                     {
                                         var (priceRatio, refPrice) = calculateFunc(product.LastPrice, product.PricesHistory);
                                         // Калькулятор возвращает current/reference; скидка % = (1 - ratio) * 100.
                                         var discontPercent = priceRatio > 0 && priceRatio < 1m
                                             ? (1m - priceRatio) * 100m
                                             : 0m;
                                         return new { product, discontPercent, refPrice };
                                     })
                                     .Where(x => x.discontPercent > 0)
                                     .Select(x => new Discont
                                     {
                                         Product = x.product,
                                         CurrentPrice = x.product.LastPrice,
                                         DiscontPercent = x.discontPercent,
                                         ReferencePrice = x.refPrice,
                                         ReferencePriceStrategy = strategy
                                     })
                                     .ToList();

            return result;
        }
    }

}
