namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class WbFilterBag : BaseDbEntity
    {
        public int FilterId { get; set; }
        public int ProductId { get; set; }
        public WbProduct? Product { get; set; }
        public WbFilter? Filter { get; set; }
    }
}
