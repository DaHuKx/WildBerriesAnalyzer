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
            var historyPrices = await Context.PricesHistory.Where(price => price.ProductId == product.Id)
                                                           .ToListAsync();

            if (historyPrices != null && !historyPrices.Any())
            {
                _notifier.Warning($"GetProductPriceHistoryAsync: Продукт '{product.Name} (Id - {product.Id})' не имеет истории цен.");
            }

            return historyPrices;
        }

        public override async Task<IEnumerable<WbPrice>> AddRangeAsync(IEnumerable<WbPrice> newEntities)
        {
            await Context.PricesHistory.AddRangeAsync(newEntities);
            await Context.SaveChangesAsync();

            return newEntities;
        }

        public async Task AddPricesFromProductsAsync(IEnumerable<WbProduct> products)
        {
            var productsIds = products.Select(p => p.IdInMarket);

            var dbProducts = await Context.Products.Where(prod => productsIds.Contains(prod.IdInMarket))
                                                   .ToListAsync();

            var prices = new List<WbPrice>();

            foreach (var product in products)
            {
                var currentProduct = dbProducts.FirstOrDefault(p => p.IdInMarket == product.IdInMarket);

                if (currentProduct is null)
                {
                    continue;
                }

                product.PriceFromInit.ProductId = currentProduct.Id;
                prices.Add(product.PriceFromInit);
            }

            await Context.PricesHistory.AddRangeAsync(prices);
            await Context.SaveChangesAsync();
        }
    }
}
