using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class MarketTypeOption : BindableBase
    {
        private bool _isSelected;

        public MarketTypeOption(MarketType marketType, string title, bool isSelected)
        {
            MarketType = marketType;
            Title = title;
            _isSelected = isSelected;
            ToggleCommand = new DelegateCommand(Toggle);
        }

        public MarketType MarketType { get; }

        public string Title { get; }

        public string BadgeLabel => MarketBadge.LabelFor(MarketType);

        public DelegateCommand ToggleCommand { get; }

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

        public Color TitleColor => ThemeColors.TextPrimary;

        public Color BadgeColor => MarketBadge.ColorFor(MarketType);

        public void Toggle() => IsSelected = !IsSelected;

        public void RefreshThemeColors()
        {
            RaisePropertyChanged(nameof(CardBackground));
            RaisePropertyChanged(nameof(CardStroke));
            RaisePropertyChanged(nameof(TitleColor));
        }
    }
}
