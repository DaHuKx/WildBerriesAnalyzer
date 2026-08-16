using System.Collections.ObjectModel;
using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.ProductDetail.Models;

namespace WildBerriesAnalyzer.Modules.ProductDetail.ViewModels
{
    public sealed class ProductDetailPageViewModel : BindableBase, INavigatedAware
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private readonly IProductsService _productsService;
        private readonly INavigationService _navigationService;
        private readonly IAdultContentPreferenceService _adultContentPreference;

        private int _productId;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _name = string.Empty;
        private string _brand = string.Empty;
        private string? _link;
        private string? _imageUrl;
        private MarketType _marketType = MarketType.Wildberries;
        private bool _isAdult;
        private bool _isAdultContentRestricted;
        private string _currentPriceText = "—";
        private string _minPriceText = "—";
        private string _maxPriceText = "—";
        private string _avgPriceText = "—";
        private string _pointsCountText = string.Empty;
        private ImageSource? _displayImage;
        private List<ProductPricePoint> _chartPoints = [];

        public ProductDetailPageViewModel(
            IProductsService productsService,
            INavigationService navigationService,
            IAdultContentPreferenceService adultContentPreference)
        {
            _productsService = productsService;
            _navigationService = navigationService;
            _adultContentPreference = adultContentPreference;

            Periods = new ObservableCollection<PricePeriodOption>(PricePeriodOption.CreateAll());

            GoBackCommand = new DelegateCommand(async () => await GoBackAsync());
            OpenWbCommand = new DelegateCommand(async () => await OpenWbAsync(), () => !string.IsNullOrWhiteSpace(Link))
                .ObservesProperty(() => Link);
            SelectPeriodCommand = new DelegateCommand<PricePeriodOption>(async p => await SelectPeriodAsync(p), _ => !IsBusy)
                .ObservesProperty(() => IsBusy);
            RetryCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);
        }

        public ObservableCollection<PricePeriodOption> Periods { get; }

        public IReadOnlyList<ProductPricePoint> ChartPoints => _chartPoints;

        public DelegateCommand GoBackCommand { get; }

        public DelegateCommand OpenWbCommand { get; }

        public DelegateCommand<PricePeriodOption> SelectPeriodCommand { get; }

        public DelegateCommand RetryCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(ShowContent));
                    RaisePropertyChanged(nameof(ShowError));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    RaisePropertyChanged(nameof(ShowError));
                    RaisePropertyChanged(nameof(ShowContent));
                }
            }
        }

        public bool ShowError => !string.IsNullOrWhiteSpace(ErrorMessage) && !IsBusy;

        public bool ShowContent => !IsBusy && string.IsNullOrWhiteSpace(ErrorMessage);

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Brand
        {
            get => _brand;
            set
            {
                if (SetProperty(ref _brand, value))
                {
                    RaisePropertyChanged(nameof(HasBrand));
                }
            }
        }

        public bool HasBrand => !string.IsNullOrWhiteSpace(Brand);

        public string? Link
        {
            get => _link;
            set => SetProperty(ref _link, value);
        }

        public string? ImageUrl
        {
            get => _imageUrl;
            set => SetProperty(ref _imageUrl, value);
        }

        public MarketType MarketType
        {
            get => _marketType;
            set
            {
                if (SetProperty(ref _marketType, value))
                {
                    RaisePropertyChanged(nameof(MarketBadgeLabel));
                    RaisePropertyChanged(nameof(MarketBadgeColor));
                    RaisePropertyChanged(nameof(OpenMarketButtonText));
                }
            }
        }

        public string MarketBadgeLabel => MarketBadge.LabelFor(MarketType);

        public Color MarketBadgeColor => MarketBadge.ColorFor(MarketType);

        public string OpenMarketButtonText => $"К товару на {MarketBadgeLabel}";

        public ImageSource? DisplayImage
        {
            get => _displayImage;
            set
            {
                if (SetProperty(ref _displayImage, value))
                {
                    RaisePropertyChanged(nameof(HasDisplayImage));
                }
            }
        }

        public bool HasDisplayImage => DisplayImage is not null;

        public bool IsAdultContentRestricted
        {
            get => _isAdultContentRestricted;
            private set
            {
                if (SetProperty(ref _isAdultContentRestricted, value))
                {
                    RaisePropertyChanged(nameof(AdultImageOpacity));
                }
            }
        }

        public double AdultImageOpacity => IsAdultContentRestricted ? 0.35 : 1d;

        public string CurrentPriceText
        {
            get => _currentPriceText;
            set => SetProperty(ref _currentPriceText, value);
        }

        public string MinPriceText
        {
            get => _minPriceText;
            set => SetProperty(ref _minPriceText, value);
        }

        public string MaxPriceText
        {
            get => _maxPriceText;
            set => SetProperty(ref _maxPriceText, value);
        }

        public string AvgPriceText
        {
            get => _avgPriceText;
            set => SetProperty(ref _avgPriceText, value);
        }

        public string PointsCountText
        {
            get => _pointsCountText;
            set => SetProperty(ref _pointsCountText, value);
        }

        public void OnNavigatedFrom(INavigationParameters parameters)
        {
        }

        public void OnNavigatedTo(INavigationParameters parameters)
        {
            if (!parameters.TryGetValue("productId", out int productId) || productId <= 0)
            {
                ErrorMessage = "Товар не найден.";
                return;
            }

            _productId = productId;
            _ = LoadAsync();
        }

        private async Task SelectPeriodAsync(PricePeriodOption? option)
        {
            if (option is null || option.IsSelected)
            {
                return;
            }

            AppLog.Action("ProductDetail", "SelectPeriod", option.Period.ToString());
            foreach (var p in Periods)
            {
                p.IsSelected = ReferenceEquals(p, option);
            }

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_productId <= 0)
            {
                return;
            }

            var period = Periods.FirstOrDefault(p => p.IsSelected)?.Period ?? PriceHistoryPeriod.Month;

            IsBusy = true;
            ErrorMessage = string.Empty;
            AppLog.Action("ProductDetail", "Load", $"id={_productId} period={period}");

            try
            {
                var history = await _productsService.GetPriceHistoryAsync(_productId, period);
                if (AdultContentAccess.IsRestricted(history.IsAdult, _adultContentPreference.ShowAdultContent))
                {
                    await AdultContentAccess.ShowRestrictedAsync();
                    await GoBackAsync();
                    return;
                }

                ApplyHistory(history);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "ProductDetail", "Load", $"id={_productId}");
                ErrorMessage = "Не удалось загрузить историю цен. Попробуйте позже.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyHistory(ProductPriceHistory history)
        {
            Name = history.Name;
            Brand = history.Brand ?? string.Empty;
            Link = history.Link;
            ImageUrl = history.ImageUrl;
            MarketType = history.MarketType;
            _isAdult = history.IsAdult;
            IsAdultContentRestricted = AdultContentAccess.IsRestricted(
                _isAdult,
                _adultContentPreference.ShowAdultContent);

            if (!string.IsNullOrWhiteSpace(history.ImageUrl) && !IsAdultContentRestricted)
            {
                DisplayImage = ImageSource.FromUri(new Uri(history.ImageUrl));
            }
            else if (!string.IsNullOrWhiteSpace(history.ImageUrl) && IsAdultContentRestricted)
            {
                DisplayImage = null;
            }
            else
            {
                DisplayImage = null;
            }

            _chartPoints = history.Points ?? [];
            RaisePropertyChanged(nameof(ChartPoints));

            var s = history.Summary;
            CurrentPriceText = FormatPrice(s.Last);
            MinPriceText = FormatPrice(s.Min);
            MaxPriceText = FormatPrice(s.Max);
            AvgPriceText = FormatPrice(s.Median);
            PointsCountText = s.Count > 0
                ? $"{s.Count} точ."
                : "Нет точек";
        }

        private static string FormatPrice(decimal? value) =>
            value is null
                ? "—"
                : string.Create(Ru, $"{value.Value:N0} ₽");

        private async Task GoBackAsync()
        {
            await _navigationService.GoBackAsync(new NavigationParameters
            {
                { KnownNavigationParameters.UseModalNavigation, true }
            });
        }

        private async Task OpenWbAsync()
        {
            if (string.IsNullOrWhiteSpace(Link))
            {
                return;
            }

            AppLog.Action("ProductDetail", "OpenWb");
            try
            {
                await Launcher.Default.OpenAsync(Link);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "ProductDetail", "OpenWb");
                // ignore launcher errors
            }
        }
    }
}
