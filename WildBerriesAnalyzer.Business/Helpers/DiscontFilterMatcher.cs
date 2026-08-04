using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// Применение пользовательского фильтра к снимку актуальных скидок.
    /// </summary>
    public static class DiscontFilterMatcher
    {
        /// <summary>
        /// Все скидки без пользовательского фильтра: лучший % на товар.
        /// </summary>
        public static List<Discont> MatchAll(IEnumerable<WbActualDiscont> stored, int? limit = null)
        {
            var matched = stored
                .Where(d => d.Product != null)
                .GroupBy(d => d.ProductId)
                .Select(g => g.OrderByDescending(d => d.DiscontPercent).First())
                .OrderByDescending(d => d.DiscontPercent)
                .Select(ToDiscont);

            return limit is > 0 ? matched.Take(limit.Value).ToList() : matched.ToList();
        }

        public static List<Discont> Match(
            WbFilter filter,
            IEnumerable<WbActualDiscont> stored,
            int? limit = null)
        {
            ArgumentNullException.ThrowIfNull(filter);

            var strategies = filter.ReferencePriceStrartegies is { Count: > 0 }
                ? filter.ReferencePriceStrartegies.ToHashSet()
                : new HashSet<ReferencePriceStrategy> { ReferencePriceStrategy.Median };

            var scopedProductIds = GetScopedProductIds(filter, stored);

            var matched = stored
                .Where(d => d.Product != null)
                .Where(d => strategies.Contains(d.ReferencePriceStrategy))
                .Where(d => scopedProductIds is null || scopedProductIds.Contains(d.ProductId))
                .Select(ToDiscont)
                .Where(filter.FilterApprovedForDiscont)
                .GroupBy(d => d.Product.Id)
                .Select(g => g.OrderByDescending(d => d.DiscontPercent).First())
                .OrderByDescending(d => d.DiscontPercent);

            return limit is > 0 ? matched.Take(limit.Value).ToList() : matched.ToList();
        }

        /// <summary>
        /// null = без ограничения по товарам (ProductsFilterType.None).
        /// </summary>
        private static HashSet<int>? GetScopedProductIds(WbFilter filter, IEnumerable<WbActualDiscont> stored)
        {
            switch (filter.ProductsFilterType)
            {
                case ProductsFilterType.OwnBag:
                {
                    return (filter.BagProducts ?? new List<WbFilterBag>())
                        .Select(b => b.ProductId)
                        .ToHashSet();
                }
                case ProductsFilterType.Categories_WhiteList:
                {
                    var white = (filter.FilterCategories ?? new List<WbFilterCategory>())
                        .Where(c => c.Type == CategoryFilterType.WhiteList)
                        .Select(c => c.CategoryId)
                        .ToHashSet();

                    return stored
                        .Where(d => d.Product?.CategoryId != null && white.Contains(d.Product.CategoryId.Value))
                        .Select(d => d.ProductId)
                        .ToHashSet();
                }
                case ProductsFilterType.Categories_BlackList:
                {
                    var black = (filter.FilterCategories ?? new List<WbFilterCategory>())
                        .Where(c => c.Type == CategoryFilterType.BlackList)
                        .Select(c => c.CategoryId)
                        .ToHashSet();

                    return stored
                        .Where(d => d.Product != null
                                    && (!d.Product.CategoryId.HasValue || !black.Contains(d.Product.CategoryId.Value)))
                        .Select(d => d.ProductId)
                        .ToHashSet();
                }
                default:
                    return null;
            }
        }

        private static Discont ToDiscont(WbActualDiscont entity)
        {
            return new Discont
            {
                Product = entity.Product,
                DiscontPercent = entity.DiscontPercent,
                ReferencePriceStrategy = entity.ReferencePriceStrategy,
                ReferencePricePeriodFrom = entity.ReferencePricePeriodFrom,
                CurrentPrice = new WbPrice
                {
                    ProductId = entity.ProductId,
                    Price = entity.CurrentPrice,
                    CheckTime = entity.CurrentPriceCheckTime ?? entity.CalculatedAt
                },
                ReferencePrice = entity.ReferencePrice is null
                    ? null
                    : new WbPrice
                    {
                        ProductId = entity.ProductId,
                        Price = entity.ReferencePrice.Value,
                        // Без fallback на CalculatedAt — иначе у агрегатов «дата» совпадает с текущей.
                        CheckTime = entity.ReferencePriceCheckTime ?? default,
                        CreatedAt = entity.ReferencePricePeriodFrom ?? default
                    }
            };
        }
    }
}
