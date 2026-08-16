namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class KnownCategoryOption
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string DisplayTitle => Name;

        public override string ToString() => DisplayTitle;
    }
}
