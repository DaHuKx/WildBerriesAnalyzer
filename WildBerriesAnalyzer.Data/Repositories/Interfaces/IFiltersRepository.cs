using System.Collections.Generic;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IFiltersRepository : IBaseRepository<WbFilter>
    {
        Task<WbFilter?> GetByUserIdAsync(int userId);
        Task<WbFilter> GetOrCreateByUserIdAsync(int userId);
        /// <summary>
        /// Фильтр пользователя с корзиной и категориями.
        /// </summary>
        Task<WbFilter?> GetFilterWithDetailsAsync(int userId);
        /// <summary>
        /// Фильтры пользователей с VK для рассылки скидок.
        /// </summary>
        Task<List<WbFilter>> GetFiltersForNotificationsAsync();
        Task<List<WbProduct>> AddProductsToUserBag(int userId, List<WbProduct> products);
        Task RemoveProductsFromUserBagAsync(int userId, IEnumerable<int> productIds);
        Task<List<WbFilterCategory>> GetFilterCategoriesAsync(int userId);
        Task AddFilterCategoryAsync(int userId, int categoryId, CategoryFilterType type);
        Task RemoveFilterCategoryAsync(int userId, int filterCategoryId);

        /// <summary>
        /// Перенести фильтр orphan-пользователя на target, если у target фильтра нет.
        /// </summary>
        Task<bool> TryReassignFilterAsync(int sourceUserId, int targetUserId);

        /// <summary>
        /// Скопировать товары корзины с source на target без дублей.
        /// </summary>
        Task MergeBagProductsAsync(int sourceUserId, int targetUserId);

        /// <summary>
        /// Удалить фильтр пользователя вместе с корзиной и категориями.
        /// </summary>
        Task DeleteFilterCascadeAsync(int userId);

        Task<List<WbCategory>?> GetAllCategoriesAsync();
    }
}

