using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WildBerriesAnalyzer.Bots.Clients.Interfaces;
using WildBerriesAnalyzer.Bots.Enums;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Bots.Services
{
    /// <summary>
    /// Забирает PriceUpdateJobs и уведомляет пользователей по уже посчитанному снимку скидок в БД.
    /// </summary>
    public sealed class CheckPriceService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
        private const int CandidateLimit = 50;
        private const int MaxDiscountsPerMessage = 10;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IClient _vkClient;
        private readonly ILogger<CheckPriceService> _logger;
        private readonly string _workerId;

        public CheckPriceService(
            IServiceScopeFactory scopeFactory,
            IEnumerable<IClient> clients,
            ILogger<CheckPriceService> logger)
        {
            _scopeFactory = scopeFactory;
            _vkClient = clients.First(c => c.BotType == BotType.Vk);
            _logger = logger;
            _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            _logger.LogInformation("CheckPriceService запущен (worker={WorkerId}).", _workerId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await ProcessNextJobAsync(stoppingToken);
                    if (!processed)
                    {
                        await Task.Delay(PollInterval, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в цикле CheckPriceService.");
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }

            _logger.LogInformation("CheckPriceService остановлен.");
        }

        private async Task<bool> ProcessNextJobAsync(CancellationToken stoppingToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IPriceUpdateJobsRepository>();
            var filtersRepository = scope.ServiceProvider.GetRequiredService<IFiltersRepository>();
            var actualDiscontsService = scope.ServiceProvider.GetRequiredService<IActualDiscontsService>();
            var notificationsRepository = scope.ServiceProvider.GetRequiredService<IDiscontNotificationsRepository>();

            var job = await jobs.ClaimNextAsync(_workerId, stoppingToken);
            if (job is null)
            {
                return false;
            }

            _logger.LogInformation("Уведомление по job #{JobId}.", job.Id);

            try
            {
                var filters = await filtersRepository.GetFiltersForNotificationsAsync();
                var notifiedUsers = 0;
                var utcNow = DateTime.UtcNow;

                foreach (var filter in filters)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(filter.User?.VkId))
                    {
                        continue;
                    }

                    var candidates = await actualDiscontsService.GetForFilterAsync(
                        filter,
                        CandidateLimit,
                        stoppingToken);

                    if (candidates.Count == 0)
                    {
                        continue;
                    }

                    var lastSent = await notificationsRepository.GetLastByUserAsync(
                        filter.UserId,
                        stoppingToken);

                    var toSend = DiscontNotificationFilter.FilterNewOrImproved(
                        candidates,
                        lastSent,
                        utcNow,
                        MaxDiscountsPerMessage);

                    if (toSend.Count == 0)
                    {
                        continue;
                    }

                    await _vkClient.SendMessageAsync(new BotMessage
                    {
                        BotType = BotType.Vk,
                        UserSocialId = filter.User.VkId!,
                        UserId = filter.UserId,
                        NewUserPlace = BotUserPlace.Menu,
                        Text = DiscontMessageBuilder.Build(toSend, "Обновление: скидки по вашему фильтру")
                    });

                    var sentRows = toSend
                        .Where(d => d.Product != null)
                        .Select(d => new DiscontNotification
                        {
                            UserId = filter.UserId,
                            ProductId = d.Product!.Id,
                            ReferencePriceStrategy = d.ReferencePriceStrategy,
                            DiscontPercent = d.DiscontPercent,
                            CurrentPrice = d.CurrentPrice?.Price ?? 0,
                            SentAt = utcNow,
                            PriceUpdateJobId = job.Id
                        });

                    await notificationsRepository.UpsertSentAsync(sentRows, stoppingToken);
                    notifiedUsers++;
                }

                await jobs.MarkProcessedAsync(job.Id, stoppingToken);
                _logger.LogInformation(
                    "Job #{JobId} Processed: уведомлено пользователей={Notified}.",
                    job.Id,
                    notifiedUsers);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job #{JobId} Failed.", job.Id);
                await jobs.MarkFailedAsync(job.Id, ex.Message, stoppingToken);
                return true;
            }
        }
    }
}
