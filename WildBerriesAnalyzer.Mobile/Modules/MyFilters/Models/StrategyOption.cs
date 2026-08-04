using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class StrategyOption : BindableBase
    {
        private bool _isSelected;

        public StrategyOption(
            ReferencePriceStrategy strategy,
            string title,
            bool isSelected,
            Action<StrategyOption>? showInfo = null)
        {
            Strategy = strategy;
            Title = title;
            _isSelected = isSelected;
            ToggleCommand = new DelegateCommand(Toggle);
            ShowInfoCommand = new DelegateCommand(() => showInfo?.Invoke(this));
        }

        public ReferencePriceStrategy Strategy { get; }

        public string Title { get; }

        public DelegateCommand ToggleCommand { get; }

        public DelegateCommand ShowInfoCommand { get; }

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

        public void Toggle() => IsSelected = !IsSelected;

        public void RefreshThemeColors()
        {
            RaisePropertyChanged(nameof(CardBackground));
            RaisePropertyChanged(nameof(CardStroke));
            RaisePropertyChanged(nameof(TitleColor));
        }
    }
}
