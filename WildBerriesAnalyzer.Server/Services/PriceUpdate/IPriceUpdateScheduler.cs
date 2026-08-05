namespace WildBerriesAnalyzer.Server.Services.PriceUpdate
{
    /// <summary>
    /// Управление циклом обновления цен: пробуждение по таймеру или по смене WB auth.
    /// </summary>
    public interface IPriceUpdateScheduler
    {
        /// <summary>
        /// Прервать текущий цикл (если идёт) и запустить обновление сразу после.
        /// </summary>
        void RequestImmediateRun(string reason);

        /// <summary>
        /// Токен текущего цикла (связан со stoppingToken). Отменяется при RequestImmediateRun.
        /// </summary>
        CancellationToken BeginCycle(CancellationToken stoppingToken);

        /// <summary>
        /// Ждать Interval либо немедленный запуск.
        /// </summary>
        Task WaitForNextTriggerAsync(TimeSpan interval, CancellationToken stoppingToken);
    }
}
