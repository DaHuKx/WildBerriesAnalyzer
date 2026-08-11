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
        private const string UserFacingProblem = "Возникла проблема. Попробуйте позже.";

        private readonly IFiltersRepository _filtersRepository;
        private readonly IProductsRepository _productsRepository;
        private readonly IWildBerriesService _wildBerriesService;
        private readonly ProductIdValidator _productIdValidator;
        private readonly BasketShareUrlValidator _basketShareUrlValidator;
        private readonly WbFilterValidator _filterValidator;

        public FiltersService(
            IFiltersRepository filtersRepository,
            IProductsRepository productsRepository,
            IWildBerriesService wildBerriesService,
            ProductIdValidator productIdValidator,
            BasketShareUrlValidator basketShareUrlValidator,
            WbFilterValidator filterValidator)
        {
            _filtersRepository = filtersRepository;
            _productsRepository = productsRepository;
            _wildBerriesService = wildBerriesService;
            _productIdValidator = productIdValidator;
            _basketShareUrlValidator = basketShareUrlValidator;
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

            // Пустой список нормализуем в null (= все стратегии).
            if (filter.ReferencePriceStrartegies is { Count: 0 })
            {
                filter.ReferencePriceStrartegies = null;
            }

            if (filter.MarketTypes is { Count: 0 })
            {
                filter.MarketTypes = null;
            }

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

            var marketIds = validIds
                .Select(id => long.Parse(id, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

            // Сначала БД — share уже известных товаров не зависит от доступности WB.
            var dbProducts = await _productsRepository.GetByMarketIdsAsync(marketIds);
            var knownIds = dbProducts.Select(p => p.IdInMarket).ToHashSet();
            var missingIds = validIds
                .Where(id => !knownIds.Contains(long.Parse(id, System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();

            if (missingIds.Count > 0)
            {
                try
                {
                    // GetProductsForIdsAsync сам режет на батчи — иначе WB отдаёт только часть.
                    var fromWb = await _wildBerriesService.GetProductsForIdsAsync(missingIds);
                    if (fromWb.Count > 0)
                    {
                        var saved = await _productsRepository.GetOrAddProducts(fromWb);
                        dbProducts.AddRange(saved.Where(p => dbProducts.All(x => x.Id != p.Id)));
                    }
                }
                catch (HttpRequestException) when (dbProducts.Count > 0)
                {
                    // Часть артикулов уже в каталоге — добавим их; остальные пропустим.
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(UserFacingProblem, ex);
                }
            }

            if (dbProducts.Count == 0)
            {
                throw new InvalidOperationException(UserFacingProblem);
            }

            var addedProducts = await _filtersRepository.AddProductsToUserBag(userId, dbProducts);
            var bagProducts = await _productsRepository.GetUserBagProductsAsync(userId);

            return new AddBagProductsResult
            {
                AddedProducts = addedProducts,
                BagProducts = bagProducts
            };
        }

        public async Task<AddBagProductsResult> AddProductsToBagFromBasketShareAsync(
            int userId,
            string shareUrlOrId)
        {
            var validation = _basketShareUrlValidator.Validate(shareUrlOrId ?? string.Empty);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.Errors.First().ErrorMessage);
            }

            if (!BasketShareUrlValidator.TryGetShareId(shareUrlOrId, out var shareId))
            {
                throw new ArgumentException(validation.Errors.FirstOrDefault()?.ErrorMessage
                    ?? "Некорректная ссылка на корзину Wildberries.");
            }

            List<string> articles;
            try
            {
                articles = await _wildBerriesService.GetArticlesFromBasketShareAsync(shareId);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(UserFacingProblem, ex);
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-кейсы (пуста / не найдена) — оставляем; технические share-basket/HTTP — прячем.
                if (IsTechnicalFailureMessage(ex.Message))
                {
                    throw new InvalidOperationException(UserFacingProblem, ex);
                }

                throw;
            }

            if (articles.Count == 0)
            {
                throw new InvalidOperationException("В общей корзине нет товаров.");
            }

            return await AddProductsToBagAsync(userId, articles);
        }

        private static bool IsTechnicalFailureMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            return message.Contains("share-basket", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("DNS", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("IPv6", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("IPv4", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("HTTP ", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("Name or service", StringComparison.OrdinalIgnoreCase);
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
