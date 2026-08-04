using System;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Временный снимок рассчитанной скидки (пересчитывается на Server после обновления цен).
    /// </summary>
    public class WbActualDiscont : BaseDbEntity
    {
        public int ProductId { get; set; }

        public int? PriceUpdateJobId { get; set; }

        public ReferencePriceStrategy ReferencePriceStrategy { get; set; }

        /// <summary>
        /// Размер скидки в процентах.
        /// </summary>
        public decimal DiscontPercent { get; set; }

        public decimal CurrentPrice { get; set; }

        public DateTime? CurrentPriceCheckTime { get; set; }

        public decimal? ReferencePrice { get; set; }

        /// <summary>
        /// Дата фиксации референсной цены (точечная стратегия) либо конец периода (агрегат).
        /// </summary>
        public DateTime? ReferencePriceCheckTime { get; set; }

        /// <summary>
        /// Начало периода для агрегатных стратегий (средняя/медиана).
        /// </summary>
        public DateTime? ReferencePricePeriodFrom { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public WbProduct Product { get; set; }

        public PriceUpdateJob? PriceUpdateJob { get; set; }
    }
}
