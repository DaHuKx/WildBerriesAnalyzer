using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Models
{
    /// <summary>
    /// Результат добавления товаров в общий каталог.
    /// </summary>
    public sealed class AddCatalogProductsResult
    {
        public required List<WbProduct> AddedProducts { get; init; }

        /// <summary>
        /// Сколько товаров удалось получить с WB (до фильтра «уже в базе»).
        /// </summary>
        public int FoundCount { get; init; }

        /// <summary>
        /// Ошибки валидации отдельных артикулов (если были).
        /// </summary>
        public string? ValidationErrors { get; init; }
    }
}
