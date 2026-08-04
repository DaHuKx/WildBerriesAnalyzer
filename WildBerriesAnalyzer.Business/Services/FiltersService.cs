using System.Text;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    public class FiltersService : IFiltersService
    {
        private readonly IFiltersRepository _filtersRepository;
        private readonly IProductsRepository _productsRepository;
        private readonly IWildBerriesService _wildBerriesService;
        private readonly ProductIdValidator _productIdValidator;
        private readonly WbFilterValidator _filterValidator;

        public FiltersService(
            IFiltersRepository filtersRepository,
            IProductsRepository productsRepository,
            IWildBerriesService wildBerriesService,
            ProductIdValidator productIdValidator,
            WbFilterValidator filterValidator)
        {
            _filtersRepository = filtersRepository;
            _productsRepository = productsRepository;
            _wildBerriesService = wildBerriesService;
            _productIdValidator = productIdValidator;
            _filterValidator = filterValidator;
        }

        public async Task<UserFilterData> GetUserFilterDataAsync(int userId)
        {
            var filter = await _filtersRepository.GetOrCreateByUserIdAsync(userId);
            var bagProducts = await _productsRepository.GetUserBagProductsAsync(userId);
            var categories = await _filtersRepository.GetFilterCategoriesAsync(userId);

            return new UserFilterData
            {
                Filter = filter,
                BagProducts = bagProducts,
                Categories = categories
            };
        }

        public async Task<WbFilter> GetOrCreateByUserIdAsync(int userId)
        {
            return await _filtersRepository.GetOrCreateByUserIdAsync(userId);
        }

        public async Task UpdateFilterAsync(WbFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            var validationResult = _filterValidator.Validate(filter);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage)));
            }

            await _filtersRepository.UpdateAsync(filter);
        }

        public async Task<List<WbProduct>> GetBagProductsAsync(int userId)
        {
            return await _productsRepository.GetUserBagProductsAsync(userId);
        }

        public async Task<AddBagProductsResult> AddProductsToBagAsync(int userId, IEnumerable<string> articleInputs)
        {
            ArgumentNullException.ThrowIfNull(articleInputs);

            var validIds = new List<string>();
            var errors = new StringBuilder();

            foreach (var raw in articleInputs)
            {
                var trimmed = raw?.Trim() ?? string.Empty;
                var validationResult = _productIdValidator.Validate(trimmed);

                if (!validationResult.IsValid)
                {
                    var message = validationResult.Errors.First().ErrorMessage;
                    errors.AppendLine($"{raw}: {message}");
                    continue;
                }

                var clean = ProductHelper.ExtractCleanArticle(trimmed);
                if (!validIds.Contains(clean))
                {
                    validIds.Add(clean);
                }
            }

            if (validIds.Count == 0)
            {
                var details = errors.Length > 0
                    ? errors.ToString().Trim()
                    : "Укажите корректные артикулы товаров.";
                throw new ArgumentException(details);
            }

            var products = await _wildBerriesService.GetProductsForIdsAsync(validIds);
            if (products.Count == 0)
            {
                throw new InvalidOperationException("Не удалось получить товары с WildBerries.");
            }

            var dbProducts = await _productsRepository.GetOrAddProducts(products);
            var addedProducts = await _filtersRepository.AddProductsToUserBag(userId, dbProducts);
            var bagProducts = await _productsRepository.GetUserBagProductsAsync(userId);

            return new AddBagProductsResult
            {
                AddedProducts = addedProducts,
                BagProducts = bagProducts
            };
        }

        public async Task RemoveProductsFromBagAsync(int userId, IEnumerable<int> productIds)
        {
            await _filtersRepository.RemoveProductsFromUserBagAsync(userId, productIds);
        }

        public async Task<List<WbFilterCategory>> GetFilterCategoriesAsync(int userId)
        {
            return await _filtersRepository.GetFilterCategoriesAsync(userId);
        }

        public async Task AddFilterCategoryAsync(int userId, int categoryId, CategoryFilterType type)
        {
            if (categoryId <= 0)
            {
                throw new ArgumentException("Укажите корректный ID категории.", nameof(categoryId));
            }

            if (!Enum.IsDefined(typeof(CategoryFilterType), type))
            {
                throw new ArgumentException("Указан некорректный тип списка категорий.", nameof(type));
            }

            await _filtersRepository.AddFilterCategoryAsync(userId, categoryId, type);
        }

        public async Task RemoveFilterCategoryAsync(int userId, int filterCategoryId)
        {
            await _filtersRepository.RemoveFilterCategoryAsync(userId, filterCategoryId);
        }
    }
}
