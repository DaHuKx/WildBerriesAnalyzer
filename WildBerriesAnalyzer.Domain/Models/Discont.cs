using System;
using System.ComponentModel.DataAnnotations.Schema;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Domain.Models
{
    /// <summary>
    /// Скидка
    /// </summary>
    public class Discont
    {
        public Discont()
        {

        }

        /// <summary>
        /// Стратегия, по которой считалась скидкой
        /// </summary>
        public ReferencePriceStrategy ReferencePriceStrategy { get; set; }

        /// <summary>
        /// Текущая цена
        /// </summary>
        public WbPrice CurrentPrice { get; set; }

        /// <summary>
        /// Цена, от которой считалась скидка
        /// </summary>
        public WbPrice? ReferencePrice { get; set; }

        /// <summary>
        /// Начало периода для агрегатных референсных стратегий.
        /// </summary>
        public DateTime? ReferencePricePeriodFrom { get; set; }

        /// <summary>
        /// Размер скидки в %
        /// </summary>
        [NotMapped]
        public decimal DiscontPercent { get; set; }

        /// <summary>
        /// Продукт
        /// </summary>
        public WbProduct Product { get; set; }
    }
}
