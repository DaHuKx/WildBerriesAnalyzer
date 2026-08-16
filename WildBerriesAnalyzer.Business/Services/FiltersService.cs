using System.Linq;
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
        private readonly IOzonService _ozonService;
        private readonly ProductIdValidator _productIdValidator;
        private readonly BasketShareUrlValidator _basketShareUrlValidator;
        private readonly WbFilterValidator _filterValidator;

        public FiltersService(
            IFiltersRepository filtersRepository,
            IProductsRepository productsRepository,
            IWildBerriesService wildBerriesService,
            IOzonService ozonService,
            ProductIdValidator productIdValidator,
            BasketShareUrlValidator basketShareUrlValidator,
            WbFilterValidator filterValidator)
        {
            _filtersRepository = filtersRepository;
            _productsRepository = productsRepository;
            _wildBerriesService = wildBerriesService;
            _ozonService = ozonService;
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
            var items = (articleInputs ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (items.Count == 0)
            {
                throw new ArgumentException("Укажите корректные артикулы товаров.");
            }

            var ozon = items.Where(ProductHelper.LooksLikeOzonProductInput).ToList();
            var wb = items.Where(x => !ProductHelper.LooksLikeOzonProductInput(x)).ToList();

            if (ozon.Count > 0 && wb.Count == 0)
            {
                return await AddProductsToBagAsync(userId, ozon, MarketType.Ozon);
            }

            if (wb.Count > 0 && ozon.Count == 0)
            {
                return await AddProductsToBagAsync(userId, wb, MarketType.Wildberries);
            }

            var ozonResult = await AddProductsToBagAsync(userId, ozon, MarketType.Ozon);
            var wbResult = await AddProductsToBagAsync(userId, wb, MarketType.Wildberries);

            return new AddBagProductsResult
            {
                AddedProducts = ozonResult.AddedProducts.Concat(wbResult.AddedProducts).ToList(),
                BagProducts = wbResult.BagProducts
            };
        }

        public async Task<AddBagProductsResult> AddProductsToBagAsync(
            int userId,
            IEnumerable<string> articleInputs,
            MarketType marketType)
        {
            if (marketType is not MarketType.Wildberries and not MarketType.Ozon)
            {
                throw new ArgumentException("Неизвестный маркетплейс.", nameof(marketType));
            }

            var validIds = new List<string>();
            var errors = new StringBuilder();

            foreach (var raw in articleInputs)
            {
                var trimmed = raw?.Trim() ?? string.Empty;
                var validationResult = _productIdValidator.Validate(trimmed, marketType);

                if (!validationResult.IsValid)
                {
                    var message = validationResult.Errors.First().ErrorMessage;
                    errors.AppendLine($"{raw}: {message}");
                    continue;
                }

                var clean = ProductHelper.ExtractCleanArticle(trimmed, marketType);
                if (!validIds.Contains(clean, StringComparer.OrdinalIgnoreCase))
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

            var numericIds = validIds
                .Where(id => long.TryParse(id, out _))
                .Select(id => long.Parse(id, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

            var dbProducts = numericIds.Count > 0
                ? await _productsRepository.GetByMarketIdsAsync(numericIds, marketType)
                : [];
            var knownIds = dbProducts.Select(p => p.IdInMarket).ToHashSet();
            var missingIds = validIds
                .Where(id =>
                    !long.TryParse(id, out var marketId) ||
                    !knownIds.Contains(marketId))
                .ToList();

            if (missingIds.Count > 0)
            {
                try
                {
                    var fromMarket = marketType == MarketType.Ozon
                        ? await FetchOzonProductsAsync(missingIds)
                        : await _wildBerriesService.GetProductsForIdsAsync(missingIds);

                    if (fromMarket.Count > 0)
                    {
                        var saved = await _productsRepository.GetOrAddProducts(fromMarket);
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
                throw new InvalidOperationException(
                    marketType == MarketType.Ozon
                        ? "Не удалось загрузить карточки товаров Ozon. Проверьте артикулы или сессию ozon-scraping-auth.json."
                        : UserFacingProblem);
            }

            var addedProducts = await _filtersRepository.AddProductsToUserBag(userId, dbProducts);
            var bagProducts = await _productsRepository.GetUserBagProductsAsync(userId);

            return new AddBagProductsResult
            {
                AddedProducts = addedProducts,
                BagProducts = bagProducts
            };
        }

        private async Task<List<WbProduct>> FetchOzonProductsAsync(IReadOnlyList<string> missingIds)
        {
            try
            {
                await _ozonService.WarmUpAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Не удалось подготовить браузер Ozon (антибот).",
                    ex);
            }

            return await _ozonService.GetProductsForIdsAsync(missingIds).ConfigureAwait(false);
        }

        public async Task<AddBagProductsResult> AddProductsToBagFromBasketShareAsync(
            int userId,
            string shareUrlOrId)
        {
            if (ProductHelper.TryExtractOzonCartShareId(shareUrlOrId, out var ozonShareToken))
            {
                return await AddProductsToBagFromOzonCartShareCoreAsync(userId, ozonShareToken);
            }

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

            return await AddProductsToBagAsync(userId, articles, MarketType.Wildberries);
        }

        private async Task<AddBagProductsResult> AddProductsToBagFromOzonCartShareCoreAsync(
            int userId,
            string shareToken)
        {
            List<string> articles;
            try
            {
                articles = await _ozonService.GetArticlesFromCartShareAsync(shareToken);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                if (IsTechnicalFailureMessage(ex.Message))
                {
                    throw new InvalidOperationException(UserFacingProblem, ex);
                }

                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException)
            {
                throw new InvalidOperationException(UserFacingProblem, ex);
            }

            if (articles.Count == 0)
            {
                throw new InvalidOperationException("В общей корзине Ozon нет товаров.");
            }

            return await AddProductsToBagAsync(userId, articles, MarketType.Ozon);
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

        public async Task<List<WbCategory>> GetKnownCategoriesAsync()
        {
            await _productsRepository.DeduplicateCategoriesByNameAsync();

            var categories = await _filtersRepository.GetAllCategoriesAsync() ?? [];
            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

            var known = await _filtersRepository.GetAllCategoriesAsync() ?? [];
            if (known.All(c => c.Id != categoryId))
            {
                throw new ArgumentException(
                    "Категория не найдена. Выберите категорию из списка известных.",
                    nameof(categoryId));
            }

            await _filtersRepository.AddFilterCategoryAsync(userId, categoryId, type);
        }

        public async Task RemoveFilterCategoryAsync(int userId, int filterCategoryId)
        {
            await _filtersRepository.RemoveFilterCategoryAsync(userId, filterCategoryId);
        }
    }
}
