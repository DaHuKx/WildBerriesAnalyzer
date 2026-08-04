namespace WildBerriesAnalyzer.Domain.Enums
{
    /// <summary>
    /// Статус outbox-задачи после обновления цен.
    /// </summary>
    public enum PriceUpdateJobStatus
    {
        /// <summary>
        /// Цены сохранены, Bots ещё не взял задачу.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Bots обрабатывает задачу (скидки + уведомления).
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Уведомления отправлены.
        /// </summary>
        Processed = 2,

        /// <summary>
        /// Ошибка обработки; можно повторить.
        /// </summary>
        Failed = 3
    }
}
