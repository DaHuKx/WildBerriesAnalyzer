using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class FilterTypeOption : BindableBase
    {
        private bool _isSelected;

        public FilterTypeOption(
            ProductsFilterType type,
            string title,
            string description,
            string iconGlyph,
            int gridRow = 0,
            int gridColumn = 0)
        {
            Type = type;
            Title = title;
            Description = description;
            IconGlyph = iconGlyph;
            GridRow = gridRow;
            GridColumn = gridColumn;
        }

        public ProductsFilterType Type { get; }

        public string Title { get; }

        public string Description { get; }

        public string IconGlyph { get; }

        public int GridRow { get; }

        public int GridColumn { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    RefreshThemeColors();
                }
            }
        }

        public Color CardBackground =>
            IsSelected ? ThemeColors.PrimarySoft : ThemeColors.Surface;

        public Brush CardStroke =>
            new SolidColorBrush(IsSelected ? ThemeColors.Primary : ThemeColors.Outline);

        public Color IconColor =>
            IsSelected ? ThemeColors.Primary : ThemeColors.TextMuted;

        public Color TitleColor =>
            IsSelected ? ThemeColors.Primary : ThemeColors.TextPrimary;

        public Color DescriptionColor =>
            IsSelected ? ThemeColors.Primary : ThemeColors.TextSecondary;

        public bool ShowCheckmark => IsSelected;

        public void RefreshThemeColors()
        {
            RaisePropertyChanged(nameof(CardBackground));
            RaisePropertyChanged(nameof(CardStroke));
            RaisePropertyChanged(nameof(IconColor));
            RaisePropertyChanged(nameof(TitleColor));
            RaisePropertyChanged(nameof(DescriptionColor));
            RaisePropertyChanged(nameof(ShowCheckmark));
        }

        public override string ToString() => Title;
    }
}
