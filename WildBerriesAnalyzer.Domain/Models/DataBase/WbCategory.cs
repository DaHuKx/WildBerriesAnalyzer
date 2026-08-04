using System.Collections.Generic;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class WbCategory : BaseDbEntity
    {
        public string Name { get; set; }
        public List<WbProduct>? Products { get; set; }
        public List<WbFilterCategory>? FiltersCategory { get; set; }
    }
}
