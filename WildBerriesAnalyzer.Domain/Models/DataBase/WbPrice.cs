using System;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Цена продукта
    /// </summary>
    public class WbPrice : BaseDbEntity
    {
        /// <summary>
        /// Id продукта
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Цена
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Время фиксирования
        /// </summary>
        public DateTime CheckTime { get; set; }

        /// <summary>
        /// Продукт
        /// </summary>
        public WbProduct Product { get; set; }
    }
}
