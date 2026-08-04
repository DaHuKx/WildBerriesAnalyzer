namespace WildBerriesAnalyzer.Server.Models
{
    public class AddProductsByArticlesRequest
    {
        public List<string> Articles { get; set; } = [];
    }

    public class AddProductsByNameRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
