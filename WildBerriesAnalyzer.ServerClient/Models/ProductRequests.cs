using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.ServerClient.Models
{
    public class AddProductsByArticlesRequest
    {
        public List<string> Articles { get; set; } = [];
    }

    public class AddProductsByNameRequest
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// null / пусто = все магазины.
        /// </summary>
        public List<MarketType>? MarketTypes { get; set; }
    }
}
