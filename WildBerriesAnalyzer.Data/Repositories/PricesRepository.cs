using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class PricesRepository : BaseRepository<WbPrice>, IPricesRepository
    {
        private readonly INotifier _notifier;

        public PricesRepository(WbDataBase context, INotifier notifier) : base(context)
        {
            _notifier = notifier;
        }

        public async Task<long> GetPricesCountAsync()
        {
            return await Context.PricesHistory.CountAsync();
        }

        public async Task<IEnumerable<WbPrice>> GetProductPricesAsync(WbProduct product)
        {
            var historyPrices = await GetProductPricesAsync(product.Id, fromUtc: null, take: null);

            if (historyPrices.Count == 0)
            {
                _notifier.Warning($"GetProductPriceHistoryAsync: Продукт '{product.Name} (Id - {product.Id})' не имеет истории цен.");
            }

            return historyPrices;
        }

        public async Task<List<WbPrice>> GetProductPricesAsync(int productId, DateTime? fromUtc, int? take)
        {
            var query = Context.PricesHistory
                .AsNoTracking()
                .Where(price => price.ProductId == productId);

            if (fromUtc is { } from)
            {
                var utc = from.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(from, DateTimeKind.Utc)
                    : from.ToUniversalTime();
                query = query.Where(price => price.CheckTime >= utc);
            }

            if (take is > 0)
            {
                // Берём последние N точек периода, затем отдаём ASC для графика.
                var latest = await query
                    .OrderByDescending(price => price.CheckTime)
                    .Take(take.Value)
                    .ToListAsync();

                latest.Reverse();
                return latest;
            }

            return await query
                .OrderBy(price => price.CheckTime)
                .ToListAsync();
        }

        public override async Task<IEnumerable<WbPrice>> AddRangeAsync(IEnumerable<WbPrice> newEntities)
        {
            await Context.PricesHistory.AddRangeAsync(newEntities);
            await Context.SaveChangesAsync();

            return newEntities;
        }

        public async Task AddPricesFromProductsAsync(IEnumerable<WbProduct> products)
        {
            var productsList = products?.ToList() ?? [];
            if (productsList.Count == 0)
            {
                return;
            }

            var productsIds = productsList.Select(p => p.IdInMarket);

            var dbProducts = await Context.Products.Where(prod => productsIds.Contains(prod.IdInMarket))
                                                   .ToListAsync();

            var prices = new List<WbPrice>();

            foreach (var product in productsList)
            {
                if (product.PriceFromInit is null || product.PriceFromInit.Price <= 0)
                {
                    continue;
                }

                var currentProduct = dbProducts.FirstOrDefault(p => p.IdInMarket == product.IdInMarket);
                if (currentProduct is null)
                {
                    continue;
                }

                var checkTime = product.PriceFromInit.CheckTime;
                if (checkTime == default)
                {
                    checkTime = DateTime.UtcNow;
                }

                prices.Add(new WbPrice
                {
                    ProductId = currentProduct.Id,
                    Price = product.PriceFromInit.Price,
                    CheckTime = checkTime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(checkTime, DateTimeKind.Utc)
                        : checkTime.ToUniversalTime()
                });
            }

            if (prices.Count == 0)
            {
                return;
            }

            await Context.PricesHistory.AddRangeAsync(prices);
            await Context.SaveChangesAsync();
        }
    }
}
