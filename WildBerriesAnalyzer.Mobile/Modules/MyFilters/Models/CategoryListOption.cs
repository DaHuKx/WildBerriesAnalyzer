using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public enum CategoryListSelection
    {
        None,
        WhiteList,
        BlackList
    }

    /// <summary>
    /// Категория в списке настроек: нейтральная / белый (зелёный) / чёрный (красный).
    /// </summary>
    public class CategoryListOption : BindableBase
    {
        private CategoryListSelection _selection;

        public CategoryListOption(int categoryId, string name, CategoryListSelection selection)
        {
            CategoryId = categoryId;
            Name = name;
            _selection = selection;
            ToggleCommand = new DelegateCommand(CycleSelection);
        }

        public int CategoryId { get; }

        public string Name { get; }

        public DelegateCommand ToggleCommand { get; }

        public CategoryListSelection Selection
        {
            get => _selection;
            set
            {
                if (SetProperty(ref _selection, value))
                {
                    RefreshThemeColors();
                }
            }
        }

        public string StatusLabel => Selection switch
        {
            CategoryListSelection.WhiteList => "Белый",
            CategoryListSelection.BlackList => "Чёрный",
            _ => string.Empty
        };

        public bool HasSelection => Selection != CategoryListSelection.None;

        public Color CardBackground => Selection switch
        {
            CategoryListSelection.WhiteList => SoftSuccess,
            CategoryListSelection.BlackList => SoftError,
            _ => ThemeColors.Surface
        };

        public Brush CardStroke => new SolidColorBrush(Selection switch
        {
            CategoryListSelection.WhiteList => ThemeColors.Success,
            CategoryListSelection.BlackList => ThemeColors.Error,
            _ => ThemeColors.Outline
        });

        public Color TitleColor => ThemeColors.TextPrimary;

        public Color StatusColor => Selection switch
        {
            CategoryListSelection.WhiteList => ThemeColors.Success,
            CategoryListSelection.BlackList => ThemeColors.Error,
            _ => ThemeColors.TextMuted
        };

        public void CycleSelection()
        {
            Selection = Selection switch
            {
                CategoryListSelection.None => CategoryListSelection.WhiteList,
                CategoryListSelection.WhiteList => CategoryListSelection.BlackList,
                _ => CategoryListSelection.None
            };
        }

        public void RefreshThemeColors()
        {
            RaisePropertyChanged(nameof(CardBackground));
            RaisePropertyChanged(nameof(CardStroke));
            RaisePropertyChanged(nameof(TitleColor));
            RaisePropertyChanged(nameof(StatusColor));
            RaisePropertyChanged(nameof(StatusLabel));
            RaisePropertyChanged(nameof(HasSelection));
        }

        private static Color SoftSuccess =>
            ThemeColors.IsDark
                ? Color.FromArgb("#14532D")
                : Color.FromArgb("#DCFCE7");

        private static Color SoftError =>
            ThemeColors.IsDark
                ? Color.FromArgb("#7F1D1D")
                : Color.FromArgb("#FEE2E2");
    }
}
