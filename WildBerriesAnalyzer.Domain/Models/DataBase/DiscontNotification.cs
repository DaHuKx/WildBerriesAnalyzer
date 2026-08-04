using System;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Последнее автоматическое уведомление о скидке для пользователя (дедуп алертов бота).
    /// </summary>
    public class DiscontNotification : BaseDbEntity
    {
        public int UserId { get; set; }

        public int ProductId { get; set; }

        public ReferencePriceStrategy ReferencePriceStrategy { get; set; }

        public decimal DiscontPercent { get; set; }

        public decimal CurrentPrice { get; set; }

        public DateTime SentAt { get; set; }

        public int? PriceUpdateJobId { get; set; }

        public WbUser? User { get; set; }

        public WbProduct? Product { get; set; }
    }
}
