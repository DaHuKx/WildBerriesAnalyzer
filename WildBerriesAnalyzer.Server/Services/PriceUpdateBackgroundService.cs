using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Consts;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.WbScraping;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Server.Options;
using WildBerriesAnalyzer.Server.Services.PriceUpdate;
using WildBerriesAnalyzer.Server.Services.VkBot;

namespace WildBerriesAnalyzer.Server.Services
{
    /// <summary>
    /// Периодически загружает цены через WB и Ozon и сохраняет их в PricesHistory.
    /// При смене Cookie/Token цикл WB прерывается и запускается заново.
    /// Перед Ozon вызывается WarmUp Chromium (если ещё не прогрет).
    /// </summary>
    public sealed class PriceUpdateBackgroundService : BackgroundService
    {
        private const string AdminWbAuthUpdateMessage =
            "Не удалось подтянуть данные из WB при обновлении цен (HTTP 498 × 5).\n" +
            "Обновите token и cookie:\n" +
            "/token <accessToken>\n" +
            "/cookie wb <cookie>";

        private const string AdminOzonAuthUpdateMessage =
            "Не удалось подтянуть данные из Ozon при обновлении цен (ошибка × 5).\n" +
            "Обновите cookie:\n" +
            "/cookie ozon <cookie>";

        /// <summary>
        /// Сколько подряд auth-ошибок (WB: только 498; Ozon: IsAuthFailure) до уведомления админа.
        /// </summary>
        private const int AuthFailureNotifyThreshold = 5;

        /// <summary>
        /// Размер батча Ozon: внутри батча карточки грузятся параллельно.
        /// </summary>
        private const int MaxBatchSize = 500;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<PriceUpdateOptions> _options;
        private readonly IPriceUpdateScheduler _scheduler;
        private readonly IWbScrapingAuthStore _authStore;
        private readonly IOzonScrapingAuthUpdater _ozonAuthUpdater;
        private readonly ILogger<PriceUpdateBackgroundService> _logger;
        private FileSystemWatcher? _authFileWatcher;
        private FileSystemWatcher? _ozonAuthFileWatcher;

        public PriceUpdateBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<PriceUpdateOptions> options,
            IPriceUpdateScheduler scheduler,
            IWbScrapingAuthStore authStore,
            IOzonScrapingAuthUpdater ozonAuthUpdater,
            ILogger<PriceUpdateBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _scheduler = scheduler;
            _authStore = authStore;
            _ozonAuthUpdater = ozonAuthUpdater;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PriceUpdateBackgroundService запущен.");

            _authStore.CredentialsChanged += OnCredentialsChanged;
            StartAuthFileWatcher();
            StartOzonAuthFileWatcher();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var options = _options.CurrentValue;
                    if (!options.Enabled)
                    {
                        _logger.LogDebug("PriceUpdate отключён (PriceUpdate:Enabled=false).");
                        await _scheduler.WaitForNextTriggerAsync(options.Interval, stoppingToken);
                        continue;
                    }

                    var cycleToken = _scheduler.BeginCycle(stoppingToken);
                    var restartRequested = false;

                    try
                    {
                        await UpdatePricesAsync(options, cycleToken);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        restartRequested = true;
                        _logger.LogInformation(
                            "Цикл обновления цен прерван из‑за смены Cookie/Token — перезапуск.");
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка обновления цен.");
                    }

                    if (restartRequested)
                    {
                        continue;
                    }

                    await _scheduler.WaitForNextTriggerAsync(options.Interval, stoppingToken);
                }
            }
            finally
            {
                _authStore.CredentialsChanged -= OnCredentialsChanged;
                StopAuthFileWatcher();
                StopOzonAuthFileWatcher();
            }

            _logger.LogInformation("PriceUpdateBackgroundService остановлен.");
        }

        private void OnCredentialsChanged(object? sender, EventArgs e) =>
            _scheduler.RequestImmediateRun("CredentialsChanged (AccessToken/Cookie)");

        private void StartAuthFileWatcher()
        {
            try
            {
                var path = _authStore.PersistFilePath;
                var directory = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                {
                    return;
                }

                Directory.CreateDirectory(directory);

                _authFileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                _authFileWatcher.Changed += OnAuthFileChanged;
                _authFileWatcher.Created += OnAuthFileChanged;
                _authFileWatcher.Renamed += OnAuthFileRenamed;

                _logger.LogInformation("Слежение за WB auth файлом: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подписаться на изменение файла WB auth.");
            }
        }

        private void StopAuthFileWatcher()
        {
            if (_authFileWatcher is null)
            {
                return;
            }

            _authFileWatcher.EnableRaisingEvents = false;
            _authFileWatcher.Changed -= OnAuthFileChanged;
            _authFileWatcher.Created -= OnAuthFileChanged;
            _authFileWatcher.Renamed -= OnAuthFileRenamed;
            _authFileWatcher.Dispose();
            _authFileWatcher = null;
        }

        private void OnAuthFileChanged(object sender, FileSystemEventArgs e) =>
            OnAuthFileTouched();

        private void OnAuthFileRenamed(object sender, RenamedEventArgs e) =>
            OnAuthFileTouched();

        private void OnAuthFileTouched()
        {
            // Bots пишет файл в другом процессе — подтянуть в память и при смене credentials перезапустить цикл.
            try
            {
                _ = _authStore.GetSnapshot();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reload WB auth после изменения файла не удался.");
            }
        }

        private void StartOzonAuthFileWatcher()
        {
            try
            {
                var path = _ozonAuthUpdater.PersistFilePath;
                var directory = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                {
                    return;
                }

                Directory.CreateDirectory(directory);

                _ozonAuthFileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                _ozonAuthFileWatcher.Changed += OnOzonAuthFileChanged;
                _ozonAuthFileWatcher.Created += OnOzonAuthFileChanged;
                _ozonAuthFileWatcher.Renamed += OnOzonAuthFileRenamed;

                _logger.LogInformation("Слежение за Ozon auth файлом: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подписаться на изменение файла Ozon auth.");
            }
        }

        private void StopOzonAuthFileWatcher()
        {
            if (_ozonAuthFileWatcher is null)
            {
                return;
            }

            _ozonAuthFileWatcher.EnableRaisingEvents = false;
            _ozonAuthFileWatcher.Changed -= OnOzonAuthFileChanged;
            _ozonAuthFileWatcher.Created -= OnOzonAuthFileChanged;
            _ozonAuthFileWatcher.Renamed -= OnOzonAuthFileRenamed;
            _ozonAuthFileWatcher.Dispose();
            _ozonAuthFileWatcher = null;
        }

        private void OnOzonAuthFileChanged(object sender, FileSystemEventArgs e) =>
            OnOzonAuthFileTouched();

        private void OnOzonAuthFileRenamed(object sender, RenamedEventArgs e) =>
            OnOzonAuthFileTouched();

        private void OnOzonAuthFileTouched()
        {
            try
            {
                if (_ozonAuthUpdater.TryReloadCookieFromDisk())
                {
                    _logger.LogInformation("Ozon Cookie перечитана с диска.");
                    _scheduler.RequestImmediateRun("Ozon CookieChanged");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reload Ozon auth после изменения файла не удался.");
            }
        }

        private async Task UpdatePricesAsync(PriceUpdateOptions options, CancellationToken stoppingToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var productsRepository = scope.ServiceProvider.GetRequiredService<IProductsRepository>();
            var pricesRepository = scope.ServiceProvider.GetRequiredService<IPricesRepository>();
            var jobsRepository = scope.ServiceProvider.GetRequiredService<IPriceUpdateJobsRepository>();
            var actualDiscontsService = scope.ServiceProvider.GetRequiredService<IActualDiscontsService>();
            var wbService = scope.ServiceProvider.GetRequiredService<IWildBerriesService>();
            var ozonService = scope.ServiceProvider.GetRequiredService<IOzonService>();
            var vkMessenger = scope.ServiceProvider.GetRequiredService<IVkCommunityMessenger>();

            var products = (await productsRepository.GetAllAsync()).ToList();
            if (products.Count == 0)
            {
                _logger.LogInformation("Нет товаров для обновления цен.");
                return;
            }

            var wbProducts = products.Where(p => p.MarketType == MarketType.Wildberries).ToList();
            var ozonProducts = products.Where(p => p.MarketType == MarketType.Ozon).ToList();

            var wbBatchSize = Math.Clamp(options.WbBatchSize, 1, MaxBatchSize);
            var ozonBatchSize = Math.Clamp(options.OzonBatchSize, 1, MaxBatchSize);
            var batchDelay = options.BatchDelay < TimeSpan.Zero ? TimeSpan.Zero : options.BatchDelay;
            var savedTotal = 0;

            _logger.LogInformation(
                "Обновление цен: всего={ProductCount} (WB={WbCount}, Ozon={OzonCount}), wbBatch={WbBatchSize}, ozonBatch={OzonBatchSize}, batchDelay={BatchDelay}",
                products.Count,
                wbProducts.Count,
                ozonProducts.Count,
                wbBatchSize,
                ozonBatchSize,
                batchDelay);

            if (wbProducts.Count > 0)
            {
                var wbResult = await ProcessMarketBatchesAsync(
                    marketLabel: "WB",
                    products: wbProducts,
                    batchSize: wbBatchSize,
                    batchDelay: batchDelay,
                    parseAsync: wbService.ParseProductsPricesAsync,
                    productsRepository: productsRepository,
                    pricesRepository: pricesRepository,
                    notifyAdminOnAuthFailure: true,
                    vkMessenger: vkMessenger,
                    stoppingToken: stoppingToken);

                savedTotal += wbResult.SavedCount;
                if (!wbResult.Completed)
                {
                    return;
                }
            }

            if (ozonProducts.Count > 0)
            {
                if (!await EnsureOzonBrowserReadyAsync(ozonService, stoppingToken))
                {
                    _logger.LogWarning(
                        "Ozon пропущен: Chromium/антибот недоступны. Цены WB сохранены: {SavedCount}. Цикл завершится без Ozon.",
                        savedTotal);
                }
                else
                {
                    var ozonResult = await ProcessMarketBatchesAsync(
                        marketLabel: "Ozon",
                        products: ozonProducts,
                        batchSize: ozonBatchSize,
                        batchDelay: batchDelay,
                        parseAsync: ozonService.ParseProductsPricesAsync,
                        productsRepository: productsRepository,
                        pricesRepository: pricesRepository,
                        notifyAdminOnAuthFailure: true,
                        vkMessenger: vkMessenger,
                        stoppingToken: stoppingToken);

                    savedTotal += ozonResult.SavedCount;
                    if (!ozonResult.Completed)
                    {
                        _logger.LogWarning(
                            "Ozon: обновление цен прервано с ошибкой. Цены сохранены (WB+Ozon частичные): {SavedCount}. Цикл завершится.",
                            savedTotal);
                    }
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

        private async Task<bool> EnsureOzonBrowserReadyAsync(
            IOzonService ozonService,
            CancellationToken stoppingToken)
        {
            _logger.LogInformation("Прогрев Chromium перед обновлением цен Ozon…");
            try
            {
                await ozonService.WarmUpAsync(stoppingToken);
                _logger.LogInformation("Chromium для Ozon готов.");
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось прогреть Chromium для Ozon.");
                return false;
            }
        }

        private async Task<MarketUpdateResult> ProcessMarketBatchesAsync(
            string marketLabel,
            IReadOnlyList<WbProduct> products,
            int batchSize,
            TimeSpan batchDelay,
            Func<IEnumerable<WbProduct>, Task<ParseProductsPricesResult>> parseAsync,
            IProductsRepository productsRepository,
            IPricesRepository pricesRepository,
            bool notifyAdminOnAuthFailure,
            IVkCommunityMessenger vkMessenger,
            CancellationToken stoppingToken)
        {
            var batches = products.Chunk(batchSize).ToArray();
            var savedTotal = 0;
            var batchIndex = 0;
            var authStreak = 0;

            _logger.LogInformation(
                "{Market}: обновление цен, товаров={ProductCount}, батчей={BatchCount}",
                marketLabel,
                products.Count,
                batches.Length);

            foreach (var batch in batches)
            {
                stoppingToken.ThrowIfCancellationRequested();
                batchIndex++;

                ParseProductsPricesResult parsed;
                while (true)
                {
                    parsed = await ParseBatchWithRetriesAsync(
                        marketLabel,
                        parseAsync,
                        batch,
                        batchIndex,
                        stoppingToken);

                    if (parsed.Success)
                    {
                        authStreak = 0;
                        break;
                    }

                    if (CountsTowardAuthNotifyStreak(marketLabel, parsed))
                    {
                        authStreak++;
                        _logger.LogWarning(
                            "{Market}: auth-сбой на батче #{BatchIndex} ({Streak}/{Threshold}): {Error}",
                            marketLabel,
                            batchIndex,
                            authStreak,
                            AuthFailureNotifyThreshold,
                            parsed.ErrorMessage);

                        if (authStreak >= AuthFailureNotifyThreshold)
                        {
                            _logger.LogWarning(
                                "{Market}: цикл прерван после {Streak} auth-ошибок подряд. Сохранено цен: {SavedCount}.",
                                marketLabel,
                                authStreak,
                                savedTotal);

                            if (notifyAdminOnAuthFailure)
                            {
                                await NotifyAdminAsync(marketLabel, vkMessenger, stoppingToken);
                            }

                            return new MarketUpdateResult(Completed: false, SavedCount: savedTotal);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    authStreak = 0;
                    _logger.LogWarning(
                        "{Market}: цикл прерван на батче #{BatchIndex}: {Error}. Сохранено цен: {SavedCount}. Pending не создаётся.",
                        marketLabel,
                        batchIndex,
                        parsed.ErrorMessage,
                        savedTotal);

                    return new MarketUpdateResult(Completed: false, SavedCount: savedTotal);
                }

                var toSave = parsed.Prices
                    .Where(p => p is not null && p.ProductId > 0)
                    .ToList();

                if (parsed.ProductsWithRefreshedMeta.Count > 0)
                {
                    foreach (var product in parsed.ProductsWithRefreshedMeta)
                    {
                        product.Category = null;
                    }

                    await productsRepository.UpdateRangeAsync(parsed.ProductsWithRefreshedMeta);
                }

                if (toSave.Count == 0)
                {
                    _logger.LogWarning(
                        "{Market}: батч #{BatchIndex} из {BatchCount} товаров не вернул цен (нет наличия), продолжаем.",
                        marketLabel,
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

            _logger.LogInformation(
                "{Market}: цены сохранены {SavedCount}/{ProductCount}.",
                marketLabel,
                savedTotal,
                products.Count);

            return new MarketUpdateResult(Completed: true, SavedCount: savedTotal);
        }

        /// <summary>
        /// WB: только HTTP 498. Ozon: любая IsAuthFailure.
        /// </summary>
        private static bool CountsTowardAuthNotifyStreak(string marketLabel, ParseProductsPricesResult parsed)
        {
            if (!parsed.IsAuthFailure)
            {
                return false;
            }

            if (string.Equals(marketLabel, "WB", StringComparison.OrdinalIgnoreCase))
            {
                return parsed.HttpStatusCode == 498;
            }

            return true;
        }

        private async Task<ParseProductsPricesResult> ParseBatchWithRetriesAsync(
            string marketLabel,
            Func<IEnumerable<WbProduct>, Task<ParseProductsPricesResult>> parseAsync,
            WbProduct[] batch,
            int batchIndex,
            CancellationToken stoppingToken)
        {
            const int maxNetworkAttempts = 3;
            ParseProductsPricesResult? last = null;

            for (var attempt = 1; attempt <= maxNetworkAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();
                last = await parseAsync(batch);
                if (last.Success || last.IsAuthFailure || !last.IsNetworkFailure)
                {
                    return last;
                }

                if (attempt >= maxNetworkAttempts)
                {
                    break;
                }

                var delay = TimeSpan.FromSeconds(5 * attempt);
                _logger.LogWarning(
                    "{Market}: батч #{BatchIndex}: сетевая ошибка (попытка {Attempt}/{Max}): {Error}. Повтор через {Delay}.",
                    marketLabel,
                    batchIndex,
                    attempt,
                    maxNetworkAttempts,
                    last.ErrorMessage,
                    delay);
                await Task.Delay(delay, stoppingToken);
            }

            return last ?? ParseProductsPricesResult.Failed("Неизвестная ошибка батча.", isNetworkFailure: true);
        }

        private async Task NotifyAdminAsync(
            string marketLabel,
            IVkCommunityMessenger vkMessenger,
            CancellationToken stoppingToken)
        {
            if (!vkMessenger.IsConfigured)
            {
                _logger.LogWarning(
                    "Не удалось уведомить админа {AdminVkId}: VkBot не настроен.",
                    AdminAccounts.VkId);
                return;
            }

            var message = string.Equals(marketLabel, "Ozon", StringComparison.OrdinalIgnoreCase)
                ? AdminOzonAuthUpdateMessage
                : AdminWbAuthUpdateMessage;

            var sent = await vkMessenger.TrySendMessageAsync(
                AdminAccounts.VkId,
                message,
                stoppingToken);

            if (sent)
            {
                _logger.LogInformation(
                    "Админу {AdminVkId} отправлено уведомление о сбое {Market}.",
                    AdminAccounts.VkId,
                    marketLabel);
            }
            else
            {
                _logger.LogWarning(
                    "Не удалось отправить админу {AdminVkId} уведомление о сбое {Market}.",
                    AdminAccounts.VkId,
                    marketLabel);
            }
        }

        private readonly record struct MarketUpdateResult(bool Completed, int SavedCount);
    }
}
