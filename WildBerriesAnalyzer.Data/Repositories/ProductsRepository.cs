using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
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

            // Категории товарам не присваиваем автоматически — только через модерацию.
            foreach (var product in productsList)
            {
                product.Category = null;
                product.CategoryId = null;
            }

            var ids = productsList.Select(p => p.IdInMarket).Distinct().ToList();
            var marketTypes = productsList.Select(p => p.MarketType).Distinct().ToList();

            var existingRows = await Context.Products
                    .Where(p => ids.Contains(p.IdInMarket) && marketTypes.Contains(p.MarketType))
                    .Select(p => new { p.MarketType, p.IdInMarket })
                    .ToListAsync();

            var existingKeys = new HashSet<(MarketType MarketType, long IdInMarket)>();
            foreach (var row in existingRows)
            {
                existingKeys.Add((row.MarketType, row.IdInMarket));
            }

            var productsToAdd = productsList
                .Where(p => !existingKeys.Contains((p.MarketType, p.IdInMarket)))
                .GroupBy(p => (p.MarketType, p.IdInMarket))
                .Select(g =>
                {
                    var product = g.First();
                    product.Category = null;
                    product.CategoryId = null;
                    return product;
                })
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

        public async Task<List<WbProduct>> GetByMarketIdsAsync(
            IEnumerable<long> marketIds,
            Domain.Enums.MarketType marketType)
        {
            var ids = marketIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<WbProduct>();
            }

            return await Context.Products
                .Where(product => product.MarketType == marketType && ids.Contains(product.IdInMarket))
                .ToListAsync();
        }

        public async Task<List<WbProduct>> GetOrAddProducts(List<WbProduct> products)
        {
            if (products is null || products.Count == 0)
            {
                return [];
            }

            // Категории товарам не присваиваем автоматически — только через модерацию.
            foreach (var product in products)
            {
                product.Category = null;
            }

            var ids = products.Select(p => p.IdInMarket).Distinct().ToList();
            var candidates = await Context.Products
                .Where(product => ids.Contains(product.IdInMarket))
                .ToListAsync();

            var existProducts = candidates
                .Where(existing => products.Any(p =>
                    p.MarketType == existing.MarketType &&
                    p.IdInMarket == existing.IdInMarket))
                .ToList();

            var existingKeys = new HashSet<(MarketType MarketType, long IdInMarket)>();
            foreach (var existing in existProducts)
            {
                existingKeys.Add((existing.MarketType, existing.IdInMarket));
            }

            var incomingByKey = products
                .GroupBy(p => (p.MarketType, p.IdInMarket))
                .ToDictionary(g => g.Key, g => g.First());

            var metaChanged = false;
            foreach (var existing in existProducts)
            {
                if (!incomingByKey.TryGetValue((existing.MarketType, existing.IdInMarket), out var incoming))
                {
                    continue;
                }

                if (existing.IsAdult != incoming.IsAdult)
                {
                    existing.IsAdult = incoming.IsAdult;
                    metaChanged = true;
                }
            }

            if (metaChanged)
            {
                await Context.SaveChangesAsync();
            }

            var productsToAdd = products
                .Where(p => !existingKeys.Contains((p.MarketType, p.IdInMarket)))
                .GroupBy(p => (p.MarketType, p.IdInMarket))
                .Select(g =>
                {
                    var product = g.First();
                    product.Category = null;
                    product.CategoryId = null;
                    return product;
                })
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
        /// Схлопывает дубликаты Categories с одинаковым именем (без учёта регистра).
        /// Переносит Product.CategoryId / FilterCategories и удаляет лишние строки.
        /// </summary>
        public async Task DeduplicateCategoriesByNameAsync()
        {
            var all = await Context.Categories.ToListAsync();
            if (all.Count == 0)
            {
                return;
            }

            var dirty = false;

            var placeholders = all.Where(c => IsPlaceholderCategoryName(c.Name)).ToList();
            if (placeholders.Count > 0)
            {
                var placeholderIds = placeholders.Select(c => c.Id).ToList();
                var productsWithPlaceholder = await Context.Products
                    .Where(p => p.CategoryId != null && placeholderIds.Contains(p.CategoryId.Value))
                    .ToListAsync();
                foreach (var product in productsWithPlaceholder)
                {
                    product.CategoryId = null;
                }

                var filtersWithPlaceholder = await Context.CategoryFilters
                    .Where(fc => placeholderIds.Contains(fc.CategoryId))
                    .ToListAsync();
                Context.CategoryFilters.RemoveRange(filtersWithPlaceholder);
                Context.Categories.RemoveRange(placeholders);
                dirty = true;
                all = all.Except(placeholders).ToList();
            }

            var groups = all
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => NormalizeCategoryName(c.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var ordered = group.OrderBy(c => c.Id).ToList();
                var keeper = ordered[0];
                var losers = ordered.Skip(1).ToList();
                var loserIds = losers.Select(c => c.Id).ToList();

                keeper.MarketType = null;
                keeper.MarketCategoryId = null;
                keeper.Name = NormalizeCategoryName(keeper.Name);
                keeper.UpdatedAt = DateTime.UtcNow;

                var productsToFix = await Context.Products
                    .Where(p => p.CategoryId != null && loserIds.Contains(p.CategoryId.Value))
                    .ToListAsync();
                foreach (var product in productsToFix)
                {
                    product.CategoryId = keeper.Id;
                }

                var filtersToFix = await Context.CategoryFilters
                    .Where(fc => loserIds.Contains(fc.CategoryId))
                    .ToListAsync();

                foreach (var filterCategory in filtersToFix)
                {
                    var alreadyOnKeeper = await Context.CategoryFilters.AnyAsync(fc =>
                        fc.FilterId == filterCategory.FilterId &&
                        fc.CategoryId == keeper.Id &&
                        fc.Type == filterCategory.Type);

                    if (alreadyOnKeeper)
                    {
                        Context.CategoryFilters.Remove(filterCategory);
                    }
                    else
                    {
                        filterCategory.CategoryId = keeper.Id;
                    }
                }

                Context.Categories.RemoveRange(losers);
                dirty = true;
            }

            foreach (var category in all)
            {
                if (Context.Entry(category).State == EntityState.Deleted)
                {
                    continue;
                }

                if (category.MarketType is not null || category.MarketCategoryId is not null)
                {
                    category.MarketType = null;
                    category.MarketCategoryId = null;
                    category.UpdatedAt = DateTime.UtcNow;
                    dirty = true;
                }
            }

            if (dirty)
            {
                await Context.SaveChangesAsync();
            }
        }

        private static bool IsPlaceholderCategoryName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            const string prefix = "Категория ";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var suffix = trimmed.Substring(prefix.Length).Trim();
            return suffix.Length > 0 && suffix.All(char.IsDigit);
        }

        private static string NormalizeCategoryName(string name) =>
            (name ?? string.Empty).Trim();

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
