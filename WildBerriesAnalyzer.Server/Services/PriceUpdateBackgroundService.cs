using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Consts;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Server.Options;
using WildBerriesAnalyzer.Server.Services.VkBot;

namespace WildBerriesAnalyzer.Server.Services
{
    /// <summary>
    /// Периодически загружает цены через WildBerriesService и сохраняет их в PricesHistory.
    /// </summary>
    public sealed class PriceUpdateBackgroundService : BackgroundService
    {
        private const string AdminAuthUpdateMessage =
            "Не удалось подтянуть данные из WB при обновлении цен.\n" +
            "Необходимо обновить token и cookie:\n" +
            "/token <accessToken>\n" +
            "/cookie <cookie>";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<PriceUpdateOptions> _options;
        private readonly ILogger<PriceUpdateBackgroundService> _logger;

        public PriceUpdateBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<PriceUpdateOptions> options,
            ILogger<PriceUpdateBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PriceUpdateBackgroundService запущен.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _options.CurrentValue;
                if (!options.Enabled)
                {
                    _logger.LogDebug("PriceUpdate отключён (PriceUpdate:Enabled=false).");
                    await DelayAsync(options.Interval, stoppingToken);
                    continue;
                }

                try
                {
                    await UpdatePricesAsync(options, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обновления цен.");
                }

                await DelayAsync(options.Interval, stoppingToken);
            }

            _logger.LogInformation("PriceUpdateBackgroundService остановлен.");
        }

        private async Task UpdatePricesAsync(PriceUpdateOptions options, CancellationToken stoppingToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var productsRepository = scope.ServiceProvider.GetRequiredService<IProductsRepository>();
            var pricesRepository = scope.ServiceProvider.GetRequiredService<IPricesRepository>();
            var jobsRepository = scope.ServiceProvider.GetRequiredService<IPriceUpdateJobsRepository>();
            var actualDiscontsService = scope.ServiceProvider.GetRequiredService<IActualDiscontsService>();
            var wbService = scope.ServiceProvider.GetRequiredService<IWildBerriesService>();
            var vkMessenger = scope.ServiceProvider.GetRequiredService<IVkCommunityMessenger>();

            var products = (await productsRepository.GetAllAsync()).ToList();
            if (products.Count == 0)
            {
                _logger.LogInformation("Нет товаров для обновления цен.");
                return;
            }

            var batchSize = Math.Clamp(options.BatchSize, 1, 500);
            var batchDelay = options.BatchDelay < TimeSpan.Zero ? TimeSpan.Zero : options.BatchDelay;
            var batches = products.Chunk(batchSize).ToArray();
            var savedTotal = 0;
            var batchIndex = 0;

            _logger.LogInformation(
                "Обновление цен: товаров={ProductCount}, batch={BatchSize}, batchDelay={BatchDelay}",
                products.Count,
                batchSize,
                batchDelay);

            foreach (var batch in batches)
            {
                stoppingToken.ThrowIfCancellationRequested();
                batchIndex++;

                var parsed = await wbService.ParseProductsPricesAsync(batch);
                if (!parsed.Success)
                {
                    _logger.LogWarning(
                        "Цикл обновления цен прерван на батче #{BatchIndex}: {Error}. Сохранено цен до сбоя: {SavedCount}. Pending не создаётся.",
                        batchIndex,
                        parsed.ErrorMessage,
                        savedTotal);

                    await NotifyAdminAsync(vkMessenger, stoppingToken);
                    return;
                }

                var toSave = parsed.Prices
                    .Where(p => p is not null && p.ProductId > 0)
                    .ToList();

                if (parsed.ProductsWithRefreshedMeta.Count > 0)
                {
                    await productsRepository.UpdateRangeAsync(parsed.ProductsWithRefreshedMeta);
                }

                if (toSave.Count == 0)
                {
                    _logger.LogWarning(
                        "Батч #{BatchIndex} из {BatchCount} товаров не вернул цен (нет наличия), продолжаем.",
                        batchIndex,
                        batch.Length);
                }
                else
                {
                    await pricesRepository.AddRangeAsync(toSave);
                    savedTotal += toSave.Count;
                }

                if (batchIndex < batches.Length && batchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(batchDelay, stoppingToken);
                }
            }

            // Сначала снимок скидок, затем outbox — иначе Bots может уведомить по старым данным.
            var discontsCount = await actualDiscontsService.RecalculateAndReplaceAsync(
                priceUpdateJobId: null,
                stoppingToken);

            var job = await jobsRepository.EnqueueCompletedAsync(products.Count, savedTotal, stoppingToken);

            _logger.LogInformation(
                "Обновление завершено: цены={SavedCount}/{ProductCount}, скидки={DiscontsCount}, Job #{JobId} → Pending.",
                savedTotal,
                products.Count,
                discontsCount,
                job.Id);
        }

        private async Task NotifyAdminAsync(IVkCommunityMessenger vkMessenger, CancellationToken stoppingToken)
        {
            if (!vkMessenger.IsConfigured)
            {
                _logger.LogWarning(
                    "Не удалось уведомить админа {AdminVkId}: VkBot не настроен.",
                    AdminAccounts.VkId);
                return;
            }

            var sent = await vkMessenger.TrySendMessageAsync(
                AdminAccounts.VkId,
                AdminAuthUpdateMessage,
                stoppingToken);

            if (sent)
            {
                _logger.LogInformation("Админу {AdminVkId} отправлено уведомление о сбое WB.", AdminAccounts.VkId);
            }
            else
            {
                _logger.LogWarning(
                    "Не удалось отправить админу {AdminVkId} уведомление о сбое WB.",
                    AdminAccounts.VkId);
            }
        }

        private static async Task DelayAsync(TimeSpan interval, CancellationToken stoppingToken)
        {
            var delay = interval <= TimeSpan.Zero ? TimeSpan.FromHours(1) : interval;
            await Task.Delay(delay, stoppingToken);
        }
    }
}
