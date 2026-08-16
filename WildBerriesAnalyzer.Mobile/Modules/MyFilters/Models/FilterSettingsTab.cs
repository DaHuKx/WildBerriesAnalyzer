using Prism.Mvvm;
using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public sealed class FilterSettingsTab : BindableBase
    {
        private bool _isSelected;

        public FilterSettingsTab(FilterSettingsSection section, string title)
        {
            Section = section;
            Title = title;
        }

        public FilterSettingsSection Section { get; }

        public string Title { get; }

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

        public Color ChipBackground =>
            IsSelected ? ThemeColors.Primary : ThemeColors.Surface;

        public Brush ChipStroke =>
            new SolidColorBrush(IsSelected ? ThemeColors.Primary : ThemeColors.Outline);

        public Color TitleColor =>
            IsSelected ? Colors.White : ThemeColors.TextPrimary;

        public void RefreshThemeColors()
        {
            RaisePropertyChanged(nameof(ChipBackground));
            RaisePropertyChanged(nameof(ChipStroke));
            RaisePropertyChanged(nameof(TitleColor));
        }

        public override string ToString() => Title;
    }
}
