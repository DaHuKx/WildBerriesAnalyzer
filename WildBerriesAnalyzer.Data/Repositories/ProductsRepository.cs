using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class ProductsRepository : BaseRepository<WbProduct>, IProductsRepository
    {
        private Random _random;


        public ProductsRepository(WbDataBase context) : base(context)
        {
            _random = new Random();
        }

        public async Task<IEnumerable<WbProduct>> GetProductsWithPricesAsync()
        {
            return await Context.Products.Include(p => p.PricesHistory)
                                         .ToListAsync();
        }

        public override Task<WbProduct> GetAsync(int id)
        {
            return Context.Products.Include(p => p.PricesHistory)
                                   .FirstOrDefaultAsync(p => p.Id == id);
        }

        public override async Task<IEnumerable<WbProduct>> AddRangeAsync(IEnumerable<WbProduct> products)
        {
            var productsList = products as IList<WbProduct> ?? products.ToList();
            if (productsList.Count == 0)
            {
                return productsList;
            }

            var ids = productsList.Select(p => p.IdInMarket).Distinct().ToList();
            var marketTypes = productsList.Select(p => p.MarketType).Distinct().ToList();

            var existingRows = await Context.Products
                    .Where(p => ids.Contains(p.IdInMarket) && marketTypes.Contains(p.MarketType))
                    .Select(p => new { p.MarketType, p.IdInMarket })
                    .ToListAsync();

            var existingKeys = new HashSet<(Domain.Enums.MarketType MarketType, long IdInMarket)>();
            foreach (var row in existingRows)
            {
                existingKeys.Add((row.MarketType, row.IdInMarket));
            }

            var productsToAdd = productsList
                .Where(p => !existingKeys.Contains((p.MarketType, p.IdInMarket)))
                .GroupBy(p => (p.MarketType, p.IdInMarket))
                .Select(g => g.First())
                .ToList();

            await Context.Products.AddRangeAsync(productsToAdd);
            await Context.SaveChangesAsync();
            await SaveInitPricesAsync(productsToAdd);

            return productsToAdd;
        }

        public async Task<IEnumerable<WbProduct>> GetProductsByNameAsync(string name)
        {
            return await Context.Products
                .AsNoTracking()
                .Where(product => product.Name.ToLower().Contains(name.ToLower()))
                .Include(product => product.PricesHistory)
                .ToListAsync();
        }

        public async Task<List<WbProduct>> GetByMarketIdsAsync(IEnumerable<long> marketIds)
        {
            var ids = marketIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<WbProduct>();
            }

            return await Context.Products
                .Where(product => ids.Contains(product.IdInMarket))
                .ToListAsync();
        }

        public async Task<List<WbProduct>> GetOrAddProducts(List<WbProduct> products)
        {
            var ids = products.Select(p => p.IdInMarket);

            var existProducts = await Context.Products.Where(product => ids.Contains(product.IdInMarket))
                                                      .ToListAsync();

            var existIds = existProducts.Select(p => p.IdInMarket).ToList();
            var incomingByMarketId = products
                .GroupBy(p => p.IdInMarket)
                .ToDictionary(g => g.Key, g => g.First());

            var metaChanged = false;
            foreach (var existing in existProducts)
            {
                if (!incomingByMarketId.TryGetValue(existing.IdInMarket, out var incoming))
                {
                    continue;
                }

                if (existing.IsAdult == incoming.IsAdult)
                {
                    continue;
                }

                existing.IsAdult = incoming.IsAdult;
                metaChanged = true;
            }

            if (metaChanged)
            {
                await Context.SaveChangesAsync();
            }

            var productsToAdd = products.Where(p => !existIds.Contains(p.IdInMarket))
                                        .GroupBy(p => p.IdInMarket)
                                        .Select(p => p.First())
                                        .ToList();

            if (productsToAdd.Count != 0)
            {
                await Context.Products.AddRangeAsync(productsToAdd);
                await Context.SaveChangesAsync();
                await SaveInitPricesAsync(productsToAdd);
            }

            existProducts.AddRange(productsToAdd);

            return existProducts;
        }

        /// <summary>
        /// Сохраняет цену из WB (PriceFromInit) сразу при добавлении товара.
        /// </summary>
        private async Task SaveInitPricesAsync(IEnumerable<WbProduct> newProducts)
        {
            var prices = new List<WbPrice>();

            foreach (var product in newProducts)
            {
                if (product.Id <= 0 || product.PriceFromInit is null)
                {
                    continue;
                }

                if (product.PriceFromInit.Price <= 0)
                {
                    continue;
                }

                var checkTime = product.PriceFromInit.CheckTime;
                if (checkTime == default)
                {
                    checkTime = DateTime.UtcNow;
                }
                else if (checkTime.Kind == DateTimeKind.Unspecified)
                {
                    checkTime = DateTime.SpecifyKind(checkTime, DateTimeKind.Utc);
                }
                else if (checkTime.Kind == DateTimeKind.Local)
                {
                    checkTime = checkTime.ToUniversalTime();
                }

                prices.Add(new WbPrice
                {
                    ProductId = product.Id,
                    Price = product.PriceFromInit.Price,
                    CheckTime = checkTime
                });
            }

            if (prices.Count == 0)
            {
                return;
            }

            await Context.PricesHistory.AddRangeAsync(prices);
            await Context.SaveChangesAsync();
        }

        public async Task<WbPrice> GetProductLastPriceAsync(int id)
        {
            if (!Context.PricesHistory.Any(p => p.ProductId == id))
            {
                return new WbPrice()
                {
                    CheckTime = DateTime.Now.ToUniversalTime(),
                    Id = 0,
                    Price = 0,
                    ProductId = id
                };
            }

            var price = await Context.PricesHistory?.Where(p => p.ProductId == id)?
                                                    .OrderBy(p => p.CheckTime)
                                                    .LastAsync();

            return price;
        }

        public async Task<IEnumerable<WbProduct>> GetRandomProductsAsync(int count)
        {
            var total = await Context.Products.CountAsync();
            if (total == 0)
            {
                return [];
            }

            if (total <= count)
            {
                return await Context.Products
                    .AsNoTracking()
                    .Include(product => product.PricesHistory)
                    .ToListAsync();
            }

            var skip = _random.Next(0, total - count + 1);

            return await Context.Products
                .AsNoTracking()
                .Include(product => product.PricesHistory)
                .OrderBy(product => product.Id)
                .Skip(skip)
                .Take(count)
                .ToListAsync();
        }

        public async Task<long> GetProductsCountAsync()
        {
            return await Context.Products.CountAsync();
        }

        public async Task<List<WbProduct>> GetUserBagProductsAsync(int userId)
        {
            var userFilter = await Context.Filters.FirstOrDefaultAsync(u => u.UserId == userId);

            if (userFilter is null)
            {
                return new List<WbProduct>();
            }

            return await Context.FilterBags.Include(b => b.Product)
                                           .Where(b => b.FilterId == userFilter.Id)
                                           .Select(f => f.Product)
                                           .ToListAsync();
        }
    }
}
