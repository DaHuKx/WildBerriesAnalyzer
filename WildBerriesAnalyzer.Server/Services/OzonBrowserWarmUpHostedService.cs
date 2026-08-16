using WildBerriesAnalyzer.Business.Services.Interfaces;

namespace WildBerriesAnalyzer.Server.Services;

/// <summary>
/// При старте Server открывает Chromium и проходит antibot Ozon,
/// чтобы первый поиск товаров не ждал холодный запуск браузера.
/// </summary>
public sealed class OzonBrowserWarmUpHostedService : BackgroundService
{
    private readonly IOzonService _ozonService;
    private readonly ILogger<OzonBrowserWarmUpHostedService> _logger;

    public OzonBrowserWarmUpHostedService(
        IOzonService ozonService,
        ILogger<OzonBrowserWarmUpHostedService> logger)
    {
        _ozonService = ozonService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Прогрев браузера Ozon: запуск Chromium…");

        try
        {
            await _ozonService.WarmUpAsync(stoppingToken);
            _logger.LogInformation("Прогрев браузера Ozon завершён, сессия готова.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Прогрев браузера Ozon отменён: Server останавливается.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось прогреть браузер Ozon при старте. Chromium откроется при первом запросе.");
        }
    }
}
