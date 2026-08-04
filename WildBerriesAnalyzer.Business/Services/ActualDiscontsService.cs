using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    public sealed class ActualDiscontsService : IActualDiscontsService
    {
        private readonly IDiscontsService _discontsService;
        private readonly IProductsRepository _productsRepository;
        private readonly IActualDiscontsRepository _actualDiscontsRepository;
        private readonly IFiltersRepository _filtersRepository;

        public ActualDiscontsService(
            IDiscontsService discontsService,
            IProductsRepository productsRepository,
            IActualDiscontsRepository actualDiscontsRepository,
            IFiltersRepository filtersRepository)
        {
            _discontsService = discontsService;
            _productsRepository = productsRepository;
            _actualDiscontsRepository = actualDiscontsRepository;
            _filtersRepository = filtersRepository;
        }

        public async Task<int> RecalculateAndReplaceAsync(
            int? priceUpdateJobId = null,
            CancellationToken cancellationToken = default)
        {
            var products = (await _productsRepository.GetProductsWithPricesAsync()).ToList();
            var calculatedAt = DateTime.UtcNow;
            var entities = new List<WbActualDiscont>();

            foreach (ReferencePriceStrategy strategy in Enum.GetValues(typeof(ReferencePriceStrategy)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var disconts = _discontsService.GetDiscontsFromProducts(products, strategy);
                foreach (var discont in disconts)
                {
                    if (discont.Product is null)
                    {
                        continue;
                    }

                    var reference = discont.ReferencePrice;
                    var isAggregateReference = reference is { Id: -1 };

                    entities.Add(new WbActualDiscont
                    {
                        ProductId = discont.Product.Id,
                        PriceUpdateJobId = priceUpdateJobId,
                        ReferencePriceStrategy = strategy,
                        DiscontPercent = discont.DiscontPercent,
                        CurrentPrice = discont.CurrentPrice?.Price ?? 0,
                        CurrentPriceCheckTime = discont.CurrentPrice?.CheckTime,
                        ReferencePrice = reference?.Price,
                        ReferencePriceCheckTime = reference?.CheckTime,
                        // Для агрегатов — период [CreatedAt; CheckTime], для точечных — только CheckTime.
                        ReferencePricePeriodFrom = isAggregateReference ? reference!.CreatedAt : null,
                        CalculatedAt = calculatedAt
                    });
                }
            }

            await _actualDiscontsRepository.ReplaceAllAsync(entities, cancellationToken);
            return entities.Count;
        }

        public async Task<List<Discont>> GetForFilterAsync(
            WbFilter filter,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            var stored = await _actualDiscontsRepository.GetAllWithProductsAsync(cancellationToken);
            return DiscontFilterMatcher.Match(filter, stored, limit);
        }

        public async Task<List<Discont>> GetForUserAsync(
            int userId,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var filter = await _filtersRepository.GetFilterWithDetailsAsync(userId);
            if (filter is null)
            {
                return new List<Discont>();
            }

            return await GetForFilterAsync(filter, limit, cancellationToken);
        }

        public async Task<List<Discont>> GetAllAsync(
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var stored = await _actualDiscontsRepository.GetAllWithProductsAsync(cancellationToken);
            return DiscontFilterMatcher.MatchAll(stored, limit);
        }

        public async Task<HomeDashboardSummary> GetHomeDashboardAsync(
            int userId,
            bool updatesEnabled,
            TimeSpan updateInterval,
            CancellationToken cancellationToken = default)
        {
            var stored = await _actualDiscontsRepository.GetAllWithProductsAsync(cancellationToken);
            var productsCount = await _productsRepository.GetProductsCountAsync();
            var allDiscountsCount = DiscontFilterMatcher.MatchAll(stored).Count;

            var filter = await _filtersRepository.GetFilterWithDetailsAsync(userId);
            var userDiscountsCount = filter is null
                ? 0
                : DiscontFilterMatcher.Match(filter, stored).Count;

            DateTime? lastUpdated = stored.Count == 0
                ? null
                : stored.Max(d => d.CalculatedAt);

            DateTime? nextUpdate = null;
            if (updatesEnabled && lastUpdated is DateTime last)
            {
                nextUpdate = last.Add(updateInterval);
            }

            return new HomeDashboardSummary
            {
                ProductsCount = productsCount,
                AllDiscountsCount = allDiscountsCount,
                UserDiscountsCount = userDiscountsCount,
                LastUpdatedAt = lastUpdated,
                NextUpdateAt = nextUpdate,
                UpdatesEnabled = updatesEnabled
            };
        }
    }
}
