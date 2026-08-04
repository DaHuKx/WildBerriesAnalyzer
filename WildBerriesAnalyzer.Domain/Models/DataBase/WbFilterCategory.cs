using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class WbFilterCategory : BaseDbEntity
    {
        public int FilterId { get; set; }
        public int CategoryId { get; set; }
        public CategoryFilterType Type { get; set; }
        public WbFilter? Filter { get; set; }
        public WbCategory? Category { get; set; }
    }
}
