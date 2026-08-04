using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Modules.Account.ViewModels;
using WildBerriesAnalyzer.Modules.Account.Views;
using WildBerriesAnalyzer.Modules.ActualDiscounts.ViewModels;
using WildBerriesAnalyzer.Modules.ActualDiscounts.Views;
using WildBerriesAnalyzer.Modules.AddProducts.ViewModels;
using WildBerriesAnalyzer.Modules.AddProducts.Views;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.Modules.MainWindow.Views;
using WildBerriesAnalyzer.Modules.MyFilters.ViewModels;
using WildBerriesAnalyzer.Modules.MyFilters.Views;
using WildBerriesAnalyzer.Modules.Products.ViewModels;
using WildBerriesAnalyzer.Modules.Products.Views;

namespace WildBerriesAnalyzer.Modules.MainWindow.ViewModels
{
    public class MainWindowPageViewModel : BindableBase, INavigatedAware
    {
        private readonly IContainerProvider _container;
        private readonly IAuthSessionService _authSessionService;

        private bool _isMenuOpen;
        private string _currentSectionTitle = "Главное меню";
        private string _welcomeText = string.Empty;
        private View? _currentContent;

        public MainWindowPageViewModel(
            IContainerProvider container,
            IAuthSessionService authSessionService)
        {
            _container = container;
            _authSessionService = authSessionService;

            WelcomeText = string.IsNullOrWhiteSpace(_authSessionService.Login)
                ? "Пользователь"
                : $"Логин: {_authSessionService.Login}";

            ToggleMenuCommand = new DelegateCommand(() => IsMenuOpen = !IsMenuOpen);
            CloseMenuCommand = new DelegateCommand(() => IsMenuOpen = false);
            NavigateCommand = new DelegateCommand<string>(Navigate);
            GoHomeCommand = new DelegateCommand(() => Navigate(NavigationNames.Home));
        }

        public string WelcomeText
        {
            get => _welcomeText;
            set => SetProperty(ref _welcomeText, value);
        }

        public string CurrentSectionTitle
        {
            get => _currentSectionTitle;
            set => SetProperty(ref _currentSectionTitle, value);
        }

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set => SetProperty(ref _isMenuOpen, value);
        }

        public View? CurrentContent
        {
            get => _currentContent;
            set => SetProperty(ref _currentContent, value);
        }

        public DelegateCommand ToggleMenuCommand { get; }

        public DelegateCommand CloseMenuCommand { get; }

        public DelegateCommand<string> NavigateCommand { get; }

        public DelegateCommand GoHomeCommand { get; }

        public void OnNavigatedTo(INavigationParameters parameters)
        {
            Navigate(NavigationNames.Home);
        }

        public void OnNavigatedFrom(INavigationParameters parameters)
        {
        }

        private void Navigate(string? navigationName)
        {
            if (string.IsNullOrWhiteSpace(navigationName))
            {
                return;
            }

            _ = NavigateAsync(navigationName);
        }

        private async Task NavigateAsync(string navigationName)
        {
            // Даём меню доехать, затем меняем контент.
            if (IsMenuOpen)
            {
                IsMenuOpen = false;
                await Task.Delay(280);
            }

            CurrentSectionTitle = navigationName switch
            {
                NavigationNames.Home => "Главное меню",
                NavigationNames.Products => "Товары",
                NavigationNames.AddProducts => "Добавление товаров",
                NavigationNames.ActualDiscounts => "Актуальные скидки",
                NavigationNames.MyFilters => "Мои фильтры",
                NavigationNames.Account => "Аккаунт",
                _ => CurrentSectionTitle
            };

            CurrentContent = navigationName switch
            {
                NavigationNames.Home => CreateHomeContent(),
                NavigationNames.Products => CreateContent<ProductsPage, ProductsPageViewModel>(),
                NavigationNames.AddProducts => CreateContent<AddProductsPage, AddProductsPageViewModel>(),
                NavigationNames.ActualDiscounts => CreateContent<ActualDiscountsPage, ActualDiscountsPageViewModel>(),
                NavigationNames.MyFilters => CreateContent<MyFiltersPage, MyFiltersPageViewModel>(),
                NavigationNames.Account => CreateContent<AccountPage, AccountPageViewModel>(),
                _ => CurrentContent
            };
        }

        private View CreateHomeContent()
        {
            var view = _container.Resolve<HomePage>();
            var viewModel = _container.Resolve<HomePageViewModel>();
            viewModel.AttachNavigator(Navigate);
            view.BindingContext = viewModel;
            return view;
        }

        private View CreateContent<TView, TViewModel>()
            where TView : View
        {
            var view = _container.Resolve<TView>();
            view.BindingContext = _container.Resolve<TViewModel>();
            return view;
        }
    }
}
