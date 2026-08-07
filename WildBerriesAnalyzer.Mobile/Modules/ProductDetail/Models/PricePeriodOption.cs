using Prism.Mvvm;
using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Modules.ProductDetail.Models
{
    public sealed class PricePeriodOption : BindableBase
    {
        private bool _isSelected;

        public required PriceHistoryPeriod Period { get; init; }

        public required string Title { get; init; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    RaisePropertyChanged(nameof(Background));
                    RaisePropertyChanged(nameof(TextColor));
                }
            }
        }

        public Color Background =>
            IsSelected
                ? Color.FromArgb("#0F766E")
                : Colors.Transparent;

        public Color TextColor =>
            IsSelected
                ? Colors.White
                : Color.FromArgb("#5B6B68");

        public static IReadOnlyList<PricePeriodOption> CreateAll() =>
        [
            new() { Period = PriceHistoryPeriod.Month, Title = "Месяц", IsSelected = true },
            new() { Period = PriceHistoryPeriod.HalfYear, Title = "Полгода" },
            new() { Period = PriceHistoryPeriod.Year, Title = "Год" },
            new() { Period = PriceHistoryPeriod.AllTime, Title = "Всё время" }
        ];
    }
}
