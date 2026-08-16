using System.Collections.Generic;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Общая категория товаров (одна на WB и Ozon), ключ — имя.
    /// </summary>
    public class WbCategory : BaseDbEntity
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Устарело: категории не привязаны к магазину. Колонка留щена для совместимости.
        /// </summary>
        public int? MarketCategoryId { get; set; }

        /// <summary>
        /// Устарело: категории не привязаны к магазину. Колонка оставлена для совместимости.
        /// </summary>
        public MarketType? MarketType { get; set; }

        public List<WbProduct>? Products { get; set; }

        public List<WbProductCategory>? ProductCategories { get; set; }

        public List<WbFilterCategory>? FiltersCategory { get; set; }
    }
}
