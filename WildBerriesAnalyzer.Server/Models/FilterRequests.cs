using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Server.Models
{
    public class AddBagProductsRequest
    {
        public List<string> Articles { get; set; } = [];
    }

    public class RemoveBagProductsRequest
    {
        public List<int> ProductIds { get; set; } = [];
    }

    public class AddFilterCategoryRequest
    {
        public int CategoryId { get; set; }

        public CategoryFilterType Type { get; set; }
    }
}
