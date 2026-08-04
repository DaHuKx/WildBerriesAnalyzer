using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class FiltersRepository : BaseRepository<WbFilter>, IFiltersRepository
    {
        public FiltersRepository(WbDataBase context) : base(context)
        {
        }

        public Task<WbFilter?> GetByUserIdAsync(int userId)
        {
            return Context.Filters.FirstOrDefaultAsync(filter => filter.UserId == userId);
        }

        public async Task<WbFilter> GetOrCreateByUserIdAsync(int userId)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);

            if (filter is null)
            {
                filter = new WbFilter
                {
                    UserId = userId
                };

                await Context.AddAsync(filter);
                await Context.SaveChangesAsync();
            }

            return filter;
        }

        public Task<WbFilter?> GetFilterWithDetailsAsync(int userId)
        {
            return Context.Filters
                .Include(f => f.User)
                .Include(f => f.BagProducts)
                .Include(f => f.FilterCategories)
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        public async Task<List<WbFilter>> GetFiltersForNotificationsAsync()
        {
            return await Context.Filters
                .Include(f => f.User)
                .Include(f => f.BagProducts)
                .Include(f => f.FilterCategories)
                .Where(f => f.User != null && f.User.VkId != null && f.User.VkId != "")
                .ToListAsync();
        }

        public async Task<List<WbProduct>> AddProductsToUserBag(int userId, List<WbProduct> products)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);
            if (filter is null)
            {
                return new List<WbProduct>();
            }

            var userBag = await Context.FilterBags.Where(u => u.FilterId == filter.Id)
                                                  .Select(ub => ub.ProductId)
                                                  .ToListAsync();

            var productsToAddInBag = products.Where(p => !userBag.Contains(p.Id)).ToList();

            if (productsToAddInBag.Count == 0)
            {
                return productsToAddInBag;
            }

            await Context.FilterBags.AddRangeAsync(productsToAddInBag.Select(p => new WbFilterBag
            {
                FilterId = filter.Id,
                ProductId = p.Id
            }));
            await Context.SaveChangesAsync();

            return productsToAddInBag;
        }

        public async Task RemoveProductsFromUserBagAsync(int userId, IEnumerable<int> productIds)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);
            if (filter is null)
            {
                return;
            }

            var ids = productIds.ToList();
            var bagItems = await Context.FilterBags
                .Where(b => b.FilterId == filter.Id && ids.Contains(b.ProductId))
                .ToListAsync();

            if (bagItems.Count == 0)
            {
                return;
            }

            Context.FilterBags.RemoveRange(bagItems);
            await Context.SaveChangesAsync();
        }

        public async Task<List<WbFilterCategory>> GetFilterCategoriesAsync(int userId)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);
            if (filter is null)
            {
                return new List<WbFilterCategory>();
            }

            return await Context.CategoryFilters
                .Include(c => c.Category)
                .Where(c => c.FilterId == filter.Id)
                .ToListAsync();
        }

        public async Task AddFilterCategoryAsync(int userId, int categoryId, CategoryFilterType type)
        {
            var filter = await GetOrCreateByUserIdAsync(userId);

            var exists = await Context.CategoryFilters
                .AnyAsync(c => c.FilterId == filter.Id && c.CategoryId == categoryId);

            if (exists)
            {
                return;
            }

            await Context.CategoryFilters.AddAsync(new WbFilterCategory
            {
                FilterId = filter.Id,
                CategoryId = categoryId,
                Type = type
            });
            await Context.SaveChangesAsync();
        }

        public async Task RemoveFilterCategoryAsync(int userId, int filterCategoryId)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);
            if (filter is null)
            {
                return;
            }

            var item = await Context.CategoryFilters
                .FirstOrDefaultAsync(c => c.Id == filterCategoryId && c.FilterId == filter.Id);

            if (item is null)
            {
                return;
            }

            Context.CategoryFilters.Remove(item);
            await Context.SaveChangesAsync();
        }

        public async Task<bool> TryReassignFilterAsync(int sourceUserId, int targetUserId)
        {
            var sourceFilter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == sourceUserId);
            if (sourceFilter is null)
            {
                return false;
            }

            var targetHasFilter = await Context.Filters.AnyAsync(f => f.UserId == targetUserId);
            if (targetHasFilter)
            {
                return false;
            }

            sourceFilter.UserId = targetUserId;
            sourceFilter.UpdatedAt = System.DateTime.UtcNow;
            await Context.SaveChangesAsync();
            return true;
        }

        public async Task MergeBagProductsAsync(int sourceUserId, int targetUserId)
        {
            var sourceFilter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == sourceUserId);
            if (sourceFilter is null)
            {
                return;
            }

            var targetFilter = await GetOrCreateByUserIdAsync(targetUserId);

            var sourceProductIds = await Context.FilterBags
                .Where(b => b.FilterId == sourceFilter.Id)
                .Select(b => b.ProductId)
                .ToListAsync();

            if (sourceProductIds.Count == 0)
            {
                return;
            }

            var targetProductIds = await Context.FilterBags
                .Where(b => b.FilterId == targetFilter.Id)
                .Select(b => b.ProductId)
                .ToListAsync();

            var toAdd = sourceProductIds
                .Where(id => !targetProductIds.Contains(id))
                .Distinct()
                .Select(id => new WbFilterBag
                {
                    FilterId = targetFilter.Id,
                    ProductId = id
                })
                .ToList();

            if (toAdd.Count == 0)
            {
                return;
            }

            await Context.FilterBags.AddRangeAsync(toAdd);
            await Context.SaveChangesAsync();
        }

        public async Task DeleteFilterCascadeAsync(int userId)
        {
            var filter = await Context.Filters.FirstOrDefaultAsync(f => f.UserId == userId);
            if (filter is null)
            {
                return;
            }

            var bags = await Context.FilterBags.Where(b => b.FilterId == filter.Id).ToListAsync();
            var categories = await Context.CategoryFilters.Where(c => c.FilterId == filter.Id).ToListAsync();

            if (bags.Count > 0)
            {
                Context.FilterBags.RemoveRange(bags);
            }

            if (categories.Count > 0)
            {
                Context.CategoryFilters.RemoveRange(categories);
            }

            Context.Filters.Remove(filter);
            await Context.SaveChangesAsync();
        }
    }
}
