namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    /// <summary>
    /// Интерфейс методов работы с WildBerries.
    /// </summary>
    public interface IWildBerriesService : IParseService
    {
        /// <summary>
        /// Артикулы (nmId) из общей корзины WB по shareId
        /// (wbx-api-gateway / share-basket). 
        /// </summary>  
        Task<List<string>> GetArticlesFromBasketShareAsync(string shareId);
    }
}
