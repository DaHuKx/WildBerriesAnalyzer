namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public enum BagSortMode
    {
        NameAsc,
        NameDesc,
        ArticleAsc,
        ArticleDesc,
        BrandAsc,
        BrandDesc
    }

    public class BagSortOption
    {
        public BagSortOption(BagSortMode mode, string title)
        {
            Mode = mode;
            Title = title;
        }

        public BagSortMode Mode { get; }

        public string Title { get; }

        public override string ToString() => Title;
    }
}
