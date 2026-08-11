using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.MainWindow.ViewModels
{
    public class HomePageViewModel : BindableBase
    {
        private readonly IDashboardClient _dashboardClient;

        private Action<string>? _navigator;
        private bool _isBusy;
        private bool _hasLoaded;
        private string _errorMessage = string.Empty;
        private string _productsCountText = "—";
        private string _userDiscountsCountText = "—";
        private string _allDiscountsCountText = "—";
        private string _lastUpdatedText = "—";
        private string _nextUpdateText = "—";

        public HomePageViewModel(IDashboardClient dashboardClient)
        {
            _dashboardClient = dashboardClient;

            RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            NavigateCommand = new DelegateCommand<string>(NavigateTo);

            _ = LoadAsync();
        }

        public string Title => "PriceLab";

        public string BrandLabel => "PRICELAB";

        public string Tagline => "Аналитика цен и скидок";

        public string GreetingText => "Добро пожаловать";

        public string Description =>
            "Отслеживает цены, считает скидки по стратегиям и показывает " +
            "релевантные предложения по вашим фильтрам — в одном месте.";

        public string HowToStep1 => "Настройте фильтры: минимальная скидка, рейтинг, отзывы и область поиска.";

        public string HowToStep2 => "Откройте «Актуальные скидки» и смотрите предложения, подходящие под ваши условия.";

        public string HowToStep3 => "Добавляйте нужные товары в каталог или корзину фильтра — система учтёт их в расчётах.";

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool HasLoaded
        {
            get => _hasLoaded;
            private set => SetProperty(ref _hasLoaded, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    RaisePropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public string ProductsCountText
        {
            get => _productsCountText;
            private set => SetProperty(ref _productsCountText, value);
        }

        public string UserDiscountsCountText
        {
            get => _userDiscountsCountText;
            private set => SetProperty(ref _userDiscountsCountText, value);
        }

        public string AllDiscountsCountText
        {
            get => _allDiscountsCountText;
            private set => SetProperty(ref _allDiscountsCountText, value);
        }

        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            private set => SetProperty(ref _lastUpdatedText, value);
        }

        public string NextUpdateText
        {
            get => _nextUpdateText;
            private set => SetProperty(ref _nextUpdateText, value);
        }

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand<string> NavigateCommand { get; }

        public void AttachNavigator(Action<string> navigator)
        {
            _navigator = navigator;
        }

        private void NavigateTo(string? section)
        {
            if (string.IsNullOrWhiteSpace(section) || _navigator is null)
            {
                return;
            }

            _navigator(section);
        }

        private async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                AppLog.Action("Home", "Load");

                var summary = await _dashboardClient.GetHomeAsync().ConfigureAwait(false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProductsCountText = FormatCount(summary.ProductsCount);
                    UserDiscountsCountText = FormatCount(summary.UserDiscountsCount);
                    AllDiscountsCountText = FormatCount(summary.AllDiscountsCount);
                    LastUpdatedText = FormatDateTime(summary.LastUpdatedAt);
                    NextUpdateText = summary.UpdatesEnabled
                        ? FormatDateTime(summary.NextUpdateAt)
                        : "Обновления отключены";
                    HasLoaded = true;
                    RaisePropertyChanged(nameof(GreetingText));
                });
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Home", "Load");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ErrorMessage = ex.Message;
                    HasLoaded = true;
                });
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        }

        private static string FormatCount(long value) =>
            value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

        private static string FormatDateTime(DateTime? utc)
        {
            if (utc is null)
            {
                return "Нет данных";
            }

            var local = utc.Value.Kind switch
            {
                DateTimeKind.Utc => utc.Value.ToLocalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime(),
                _ => utc.Value
            };

            return local.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
        }

        // Used by XAML CommandParameter bindings for quick links.
        public string ProductsNav => NavigationNames.Products;

        public string DiscountsNav => NavigationNames.ActualDiscounts;

        public string FiltersNav => NavigationNames.MyFilters;

        public string AddProductsNav => NavigationNames.AddProducts;
    }
}
