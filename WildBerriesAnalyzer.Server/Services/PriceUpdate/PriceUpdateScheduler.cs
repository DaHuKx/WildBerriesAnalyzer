namespace WildBerriesAnalyzer.Server.Services.PriceUpdate
{
    public sealed class PriceUpdateScheduler : IPriceUpdateScheduler, IDisposable
    {
        private readonly object _sync = new();
        private readonly ILogger<PriceUpdateScheduler> _logger;

        private TaskCompletionSource _wake = CreateWake();
        private CancellationTokenSource? _cycleCts;
        private int _immediatePending;
        private string? _lastReason;

        public PriceUpdateScheduler(ILogger<PriceUpdateScheduler> logger)
        {
            _logger = logger;
        }

        public void RequestImmediateRun(string reason)
        {
            Interlocked.Exchange(ref _immediatePending, 1);
            _lastReason = reason;

            lock (_sync)
            {
                try
                {
                    _cycleCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                var previous = _wake;
                _wake = CreateWake();
                previous.TrySetResult();
            }

            _logger.LogInformation(
                "Запрошен немедленный перезапуск обновления цен: {Reason}",
                reason);
        }

        public CancellationToken BeginCycle(CancellationToken stoppingToken)
        {
            lock (_sync)
            {
                _cycleCts?.Dispose();
                _cycleCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                return _cycleCts.Token;
            }
        }

        public async Task WaitForNextTriggerAsync(TimeSpan interval, CancellationToken stoppingToken)
        {
            if (ConsumeImmediate())
            {
                _logger.LogInformation(
                    "Пропуск ожидания интервала — немедленный запуск ({Reason}).",
                    _lastReason ?? "auth");
                return;
            }

            var delay = interval <= TimeSpan.Zero ? TimeSpan.FromHours(1) : interval;
            Task wakeTask;
            lock (_sync)
            {
                wakeTask = _wake.Task;
            }

            var delayTask = Task.Delay(delay, stoppingToken);
            var completed = await Task.WhenAny(wakeTask, delayTask).ConfigureAwait(false);

            if (completed == wakeTask || ConsumeImmediate())
            {
                _logger.LogInformation(
                    "Ожидание интервала прервано — немедленный запуск ({Reason}).",
                    _lastReason ?? "auth");
                return;
            }

            await delayTask.ConfigureAwait(false);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _cycleCts?.Dispose();
                _cycleCts = null;
            }
        }

        private bool ConsumeImmediate() =>
            Interlocked.Exchange(ref _immediatePending, 0) == 1;

        private static TaskCompletionSource CreateWake() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
