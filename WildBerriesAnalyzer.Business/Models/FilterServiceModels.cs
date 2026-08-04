using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Models
{
    public class UserFilterData
    {
        public required WbFilter Filter { get; init; }

        public required List<WbProduct> BagProducts { get; init; }

        public required List<WbFilterCategory> Categories { get; init; }
    }

    public class AddBagProductsResult
    {
        public required List<WbProduct> AddedProducts { get; init; }

        public required List<WbProduct> BagProducts { get; init; }
    }
}
