using Prism.Commands;
using Prism.Mvvm;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class FilterCategoryItem : BindableBase
    {
        public int Id { get; init; }

        public int CategoryId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string DisplayTitle => string.IsNullOrWhiteSpace(Name)
            ? $"Категория #{CategoryId}"
            : Name;

        public DelegateCommand? RemoveCommand { get; set; }
    }
}
