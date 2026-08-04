using System;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Outbox: сигнал Server → Bots «цены и снимок скидок готовы, можно уведомить».
    /// </summary>
    public class PriceUpdateJob : BaseDbEntity
    {
        public PriceUpdateJobStatus Status { get; set; } = PriceUpdateJobStatus.Pending;

        /// <summary>
        /// Когда Server завершил полный проход обновления цен.
        /// </summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>
        /// Сколько товаров участвовало в обновлении.
        /// </summary>
        public int ProductsCount { get; set; }

        /// <summary>
        /// Сколько цен реально сохранено.
        /// </summary>
        public int PricesSavedCount { get; set; }

        /// <summary>
        /// Когда Bots забрал задачу в работу.
        /// </summary>
        public DateTime? LockedAt { get; set; }

        /// <summary>
        /// Идентификатор воркера Bots (hostname / instance id).
        /// </summary>
        public string? LockedBy { get; set; }

        /// <summary>
        /// Когда обработка успешно завершена.
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Число попыток обработки.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Последняя ошибка (если Status = Failed).
        /// </summary>
        public string? LastError { get; set; }
    }
}
