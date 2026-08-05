using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IFiltersService
    {
        Task<UserFilterData> GetUserFilterDataAsync(int userId);

        Task<WbFilter> GetOrCreateByUserIdAsync(int userId);

        Task UpdateFilterAsync(WbFilter filter);

        Task<List<WbProduct>> GetBagProductsAsync(int userId);

        Task<AddBagProductsResult> AddProductsToBagAsync(int userId, IEnumerable<string> articleInputs);

        /// <summary>
        /// Добавить в корзину товары из общей корзины WB (ссылка с shareId).
        /// </summary>
        Task<AddBagProductsResult> AddProductsToBagFromBasketShareAsync(int userId, string shareUrlOrId);

        Task RemoveProductsFromBagAsync(int userId, IEnumerable<int> productIds);

        Task<List<WbFilterCategory>> GetFilterCategoriesAsync(int userId);

        Task AddFilterCategoryAsync(int userId, int categoryId, CategoryFilterType type);

        Task RemoveFilterCategoryAsync(int userId, int filterCategoryId);
    }
}
