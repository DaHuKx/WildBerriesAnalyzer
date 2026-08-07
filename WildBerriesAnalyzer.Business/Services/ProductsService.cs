using System.Text;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    public class ProductsService : IProductsService
    {
        /// <summary>Защита VDS: максимум точек в ответе (сутки × годы при часовом обновлении).</summary>
        private const int MaxPricePoints = 5000;

        private readonly IProductsRepository _productsRepository;
        private readonly IPricesRepository _pricesRepository;
        private readonly IWildBerriesService _wildBerriesService;
        private readonly ProductIdValidator _productIdValidator;
        private readonly ProductNameValidator _productNameValidator;

        public ProductsService(
            IProductsRepository productsRepository,
            IPricesRepository pricesRepository,
            IWildBerriesService wildBerriesService,
            ProductIdValidator productIdValidator,
            ProductNameValidator productNameValidator)
        {
            _productsRepository = productsRepository;
            _pricesRepository = pricesRepository;
            _wildBerriesService = wildBerriesService;
            _productIdValidator = productIdValidator;
            _productNameValidator = productNameValidator;
        }

        public async Task<WbProduct?> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("Идентификатор товара должен быть больше нуля.", nameof(id));
            }

            return await _productsRepository.GetAsync(id);
        }

        public async Task<List<WbProduct>> GetRandomAsync(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentException("Количество товаров должно быть больше нуля.", nameof(count));
            }

            if (count > 100)
            {
                throw new ArgumentException("Количество товаров не может превышать 100.", nameof(count));
            }

            var products = await _productsRepository.GetRandomProductsAsync(count);
            var list = products?.ToList() ?? [];
            await EnsurePricesHistoryAsync(list);
            return list;
        }

        public async Task<List<WbProduct>> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Название для поиска не может быть пустым.", nameof(name));
            }

            var products = await _productsRepository.GetProductsByNameAsync(name.Trim());
            var list = products?.ToList() ?? [];
            await EnsurePricesHistoryAsync(list);
            return list;
        }

        private async Task EnsurePricesHistoryAsync(List<WbProduct> products)
        {
            foreach (var product in products)
            {
                if (product.PricesHistory is { Count: > 0 })
                {
                    continue;
                }

                var lastPrice = await _productsRepository.GetProductLastPriceAsync(product.Id);
                product.PricesHistory = [lastPrice];
            }
        }

        public async Task<long> GetCountAsync()
        {
            return await _productsRepository.GetProductsCountAsync();
        }

        public async Task<WbPrice> GetLastPriceAsync(int productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentException("Идентификатор товара должен быть больше нуля.", nameof(productId));
            }

            var product = await _productsRepository.GetAsync(productId);
            if (product is null)
            {
                throw new KeyNotFoundException($"Товар с Id={productId} не найден.");
            }

            return await _productsRepository.GetProductLastPriceAsync(productId);
        }

        public async Task<ProductPriceHistory> GetPriceHistoryAsync(int productId, PriceHistoryPeriod period)
        {
            if (productId <= 0)
            {
                throw new ArgumentException("Идентификатор товара должен быть больше нуля.", nameof(productId));
            }

            if (!Enum.IsDefined(period))
            {
                throw new ArgumentException("Некорректный период истории цен.", nameof(period));
            }

            var product = await _productsRepository.GetAsync(productId);
            if (product is null)
            {
                throw new KeyNotFoundException($"Товар с Id={productId} не найден.");
            }

            var toUtc = DateTime.UtcNow;
            var fromUtc = ResolvePeriodStartUtc(period, toUtc);

            var prices = await _pricesRepository.GetProductPricesAsync(
                productId,
                fromUtc,
                take: MaxPricePoints);

            var points = prices
                .Select(p => new ProductPricePoint
                {
                    Price = p.Price,
                    CheckTime = p.CheckTime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(p.CheckTime, DateTimeKind.Utc)
                        : p.CheckTime.ToUniversalTime()
                })
                .ToList();

            return new ProductPriceHistory
            {
                ProductId = product.Id,
                IdInMarket = product.IdInMarket,
                Name = product.Name ?? string.Empty,
                Brand = product.Brand,
                Link = product.Link,
                ImageUrl = product.ImageUrl,
                Period = period,
                PeriodFromUtc = fromUtc,
                PeriodToUtc = toUtc,
                Points = points,
                Summary = BuildSummary(points)
            };
        }

        private static DateTime? ResolvePeriodStartUtc(PriceHistoryPeriod period, DateTime toUtc)
        {
            return period switch
            {
                PriceHistoryPeriod.Month => toUtc.AddDays(-30),
                PriceHistoryPeriod.HalfYear => toUtc.AddDays(-182),
                PriceHistoryPeriod.Year => toUtc.AddDays(-365),
                PriceHistoryPeriod.AllTime => null,
                _ => toUtc.AddDays(-30)
            };
        }

        private static ProductPriceHistorySummary BuildSummary(IReadOnlyList<ProductPricePoint> points)
        {
            if (points.Count == 0)
            {
                return new ProductPriceHistorySummary();
            }

            var last = points[^1];
            return new ProductPriceHistorySummary
            {
                Count = points.Count,
                Min = points.Min(p => p.Price),
                Max = points.Max(p => p.Price),
                Median = CalculateMedian(points),
                Last = last.Price,
                LastCheckTime = last.CheckTime
            };
        }

        private static decimal? CalculateMedian(IReadOnlyList<ProductPricePoint> points)
        {
            var sorted = points
                .Select(p => p.Price)
                .Where(p => p > 0)
                .OrderBy(p => p)
                .ToArray();

            if (sorted.Length == 0)
            {
                return null;
            }

            var mid = sorted.Length / 2;
            var median = sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2m
                : sorted[mid];
            return Math.Round(median, 2, MidpointRounding.AwayFromZero);
        }

        public List<WbProduct> FilterProductsByName(IEnumerable<WbProduct> products, string productName)
        {
            if (products is null)
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                return products is ICollection<WbProduct> col
                    ? [.. col]
                    : products.ToList();
            }

            var result = new List<WbProduct>();
            var comparison = StringComparison.OrdinalIgnoreCase;

            foreach (var product in products)
            {
                if (product.Name is not null &&
                    product.Name.Contains(productName, comparison))
                {
                    result.Add(product);
                }
            }

            return result;
        }

        public async Task<List<WbProduct>> SearchOnWildBerriesAsync(string name)
        {
            var query = ValidateProductName(name);
            return await _wildBerriesService.ParseProductsAsync(query);
        }

        public async Task<AddCatalogProductsResult> AddByArticlesAsync(IEnumerable<string> articleInputs)
        {
            ArgumentNullException.ThrowIfNull(articleInputs);

            var validIds = new List<string>();
            var errors = new StringBuilder();

            foreach (var raw in articleInputs)
            {
                var trimmed = raw?.Trim() ?? string.Empty;
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var validationResult = _productIdValidator.Validate(trimmed);
                if (!validationResult.IsValid)
                {
                    errors.AppendLine($"{raw}: {validationResult.Errors.First().ErrorMessage}");
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
                    : "Укажите корректные артикулы или ссылки на товары.";
                throw new ArgumentException(details);
            }

            var products = await _wildBerriesService.GetProductsForIdsAsync(validIds);
            if (products.Count == 0)
            {
                throw new InvalidOperationException("Не удалось получить товары с WildBerries.");
            }

            var added = (await _productsRepository.AddRangeAsync(products)).ToList();
            return new AddCatalogProductsResult
            {
                AddedProducts = added,
                FoundCount = products.Count,
                ValidationErrors = errors.Length > 0 ? errors.ToString().Trim() : null
            };
        }

        public async Task<AddCatalogProductsResult> AddByNameAsync(string name)
        {
            var query = ValidateProductName(name);
            var products = await _wildBerriesService.ParseProductsAsync(query);
            if (products.Count == 0)
            {
                throw new InvalidOperationException("По запросу ничего не найдено на WildBerries.");
            }

            var added = (await _productsRepository.AddRangeAsync(products)).ToList();
            return new AddCatalogProductsResult
            {
                AddedProducts = added,
                FoundCount = products.Count
            };
        }

        private string ValidateProductName(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            var validationResult = _productNameValidator.Validate(trimmed);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));
            }

            return trimmed;
        }
    }
}
