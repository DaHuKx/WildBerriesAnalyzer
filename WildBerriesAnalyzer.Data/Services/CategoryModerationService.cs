using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Services
{
    /// <summary>
    /// Очередь модерации: товары без категории → назначение одной или нескольких категорий.
    /// </summary>
    public class CategoryModerationService
    {
        private readonly WbDataBase _db;

        public CategoryModerationService(WbDataBase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Task<int> CountUncategorizedAsync() =>
            _db.Products.AsNoTracking().CountAsync(p => p.CategoryId == null);

        public async Task<WbProduct?> GetNextUncategorizedAsync()
        {
            return await _db.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == null)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<WbCategory>> GetCategoriesAsync()
        {
            return await _db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<WbProduct>> GetUncategorizedProductsAsync()
        {
            return await _db.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == null)
                .OrderBy(p => p.Id)
                .ToListAsync();
        }

        /// <summary>
        /// Создаёт отсутствующие категории по имени, привязывает выбранные к товару, ставит CategoryId = первая.
        /// </summary>
        public async Task AssignAsync(int productId, IReadOnlyList<int> selectedCategoryIds, IReadOnlyList<string>? newCategoryNames = null)
        {
            await AssignManyAsync(new[] { productId }, selectedCategoryIds, newCategoryNames);
        }

        /// <summary>
        /// Массовое присвоение одних и тех же категорий нескольким товарам.
        /// </summary>
        public async Task<int> AssignManyAsync(
            IReadOnlyList<int> productIds,
            IReadOnlyList<int> selectedCategoryIds,
            IReadOnlyList<string>? newCategoryNames = null)
        {
            if (productIds == null || productIds.Count == 0)
            {
                throw new InvalidOperationException("Выберите хотя бы один товар.");
            }

            var distinctProductIds = productIds.Where(id => id > 0).Distinct().ToList();
            if (distinctProductIds.Count == 0)
            {
                throw new InvalidOperationException("Выберите хотя бы один товар.");
            }

            var validIds = await ResolveCategoryIdsAsync(selectedCategoryIds, newCategoryNames);

            var products = await _db.Products
                .Include(p => p.ProductCategories)
                .Where(p => distinctProductIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count == 0)
            {
                throw new InvalidOperationException("Товары не найдены.");
            }

            foreach (var product in products)
            {
                var existingLinks = product.ProductCategories ?? new List<WbProductCategory>();
                if (existingLinks.Count > 0)
                {
                    _db.ProductCategories.RemoveRange(existingLinks);
                }

                foreach (var categoryId in validIds)
                {
                    _db.ProductCategories.Add(new WbProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = categoryId
                    });
                }

                product.CategoryId = validIds[0];
                product.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return products.Count;
        }

        private async Task<List<int>> ResolveCategoryIdsAsync(
            IReadOnlyList<int> selectedCategoryIds,
            IReadOnlyList<string>? newCategoryNames)
        {
            var resolvedIds = new List<int>();
            if (selectedCategoryIds != null)
            {
                foreach (var id in selectedCategoryIds.Distinct())
                {
                    if (id > 0)
                    {
                        resolvedIds.Add(id);
                    }
                }
            }

            if (newCategoryNames != null)
            {
                foreach (var raw in newCategoryNames)
                {
                    var name = NormalizeName(raw);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var existing = await _db.Categories
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

                    if (existing is null)
                    {
                        existing = new WbCategory
                        {
                            Name = name,
                            MarketType = null,
                            MarketCategoryId = null
                        };
                        _db.Categories.Add(existing);
                        await _db.SaveChangesAsync();
                    }

                    if (!resolvedIds.Contains(existing.Id))
                    {
                        resolvedIds.Add(existing.Id);
                    }
                }
            }

            if (resolvedIds.Count == 0)
            {
                throw new InvalidOperationException("Выберите хотя бы одну категорию.");
            }

            var validIds = await _db.Categories
                .Where(c => resolvedIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (validIds.Count == 0)
            {
                throw new InvalidOperationException("Выбранные категории не найдены.");
            }

            return validIds;
        }

        private static string NormalizeName(string? name) =>
            (name ?? string.Empty).Trim();
    }
}
