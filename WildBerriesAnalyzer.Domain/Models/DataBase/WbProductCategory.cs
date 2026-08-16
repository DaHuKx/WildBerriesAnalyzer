using System;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Связь товара с категорией (у товара может быть несколько категорий).
    /// </summary>
    public class WbProductCategory : BaseDbEntity
    {
        public int ProductId { get; set; }

        public int CategoryId { get; set; }

        public WbProduct? Product { get; set; }

        public WbCategory? Category { get; set; }
    }
}
