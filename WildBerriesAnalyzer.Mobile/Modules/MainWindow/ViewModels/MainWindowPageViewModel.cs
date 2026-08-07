using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Settings.ViewModels;
using WildBerriesAnalyzer.Modules.Settings.Views;
using WildBerriesAnalyzer.Modules.ActualDiscounts.ViewModels;
using WildBerriesAnalyzer.Modules.ActualDiscounts.Views;
using WildBerriesAnalyzer.Modules.AddProducts.ViewModels;
using WildBerriesAnalyzer.Modules.AddProducts.Views;
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
        private readonly IWbShareToBagService _shareToBagService;

        private bool _isMenuOpen;
        private string _currentSectionTitle = "Главное меню";
        private View? _currentContent;
        private int _shareProcessing;
        private bool _isActive;

        public MainWindowPageViewModel(
            IContainerProvider container,
            IWbShareToBagService shareToBagService)
        {
            _container = container;
            _shareToBagService = shareToBagService;

            ToggleMenuCommand = new DelegateCommand(() => IsMenuOpen = !IsMenuOpen);
            CloseMenuCommand = new DelegateCommand(() => IsMenuOpen = false);
            NavigateCommand = new DelegateCommand<string>(Navigate);
            GoHomeCommand = new DelegateCommand(() => Navigate(NavigationNames.Home));
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
            _isActive = true;
            _shareToBagService.PendingShareAvailable -= OnPendingShareAvailable;
            _shareToBagService.PendingShareAvailable += OnPendingShareAvailable;
            _ = ArriveAsync();
        }

        public void OnNavigatedFrom(INavigationParameters parameters)
        {
            _isActive = false;
            _shareToBagService.PendingShareAvailable -= OnPendingShareAvailable;
        }

        private void OnPendingShareAvailable(object? sender, EventArgs e)
        {
            if (!_isActive)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => _ = HandleIncomingShareAsync());
        }

        private async Task ArriveAsync()
        {
            try
            {
                if (_shareToBagService.HasPending)
                {
                    await HandleIncomingShareAsync();
                    return;
                }

                // Не сбрасываем секцию при возврате с ProductDetail / других страниц стека.
                if (CurrentContent is null)
                {
                    Navigate(NavigationNames.Home);
                }
            }
            catch
            {
                if (CurrentContent is null)
                {
                    Navigate(NavigationNames.Home);
                }
            }
        }

        private async Task HandleIncomingShareAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (Interlocked.Exchange(ref _shareProcessing, 1) == 1)
            {
                return;
            }

            try
            {
                if (!_shareToBagService.HasPending)
                {
                    return;
                }

                var result = await _shareToBagService.TryProcessPendingAsync();
                if (!_isActive)
                {
                    return;
                }

                if (result is null)
                {
                    // Ещё нет сессии — ждём логин; pending остаётся в store.
                    if (CurrentContent is null)
                    {
                        Navigate(NavigationNames.Home);
                    }

                    return;
                }

                await NavigateAsync(NavigationNames.MyFilters);

                if (CurrentContent?.BindingContext is not MyFiltersPageViewModel filtersVm)
                {
                    return;
                }

                await filtersVm.LoadIfNeededAsync();
                filtersVm.ShowOwnBagSection();

                if (result.IsError)
                {
                    filtersVm.ErrorMessage = result.Message;
                }
                else
                {
                    filtersVm.StatusMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Share-to-bag failed: {ex}");
                try
                {
                    if (_isActive && CurrentContent is null)
                    {
                        Navigate(NavigationNames.Home);
                    }
                }
                catch
                {
                    // ignore
                }
            }
            finally
            {
                Interlocked.Exchange(ref _shareProcessing, 0);
                if (_isActive && _shareToBagService.HasPending)
                {
                    MainThread.BeginInvokeOnMainThread(() => _ = HandleIncomingShareAsync());
                }
            }
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
                NavigationNames.Settings => "Настройки",
                _ => CurrentSectionTitle
            };

            CurrentContent = navigationName switch
            {
                NavigationNames.Home => CreateHomeContent(),
                NavigationNames.Products => CreateContent<ProductsPage, ProductsPageViewModel>(),
                NavigationNames.AddProducts => CreateContent<AddProductsPage, AddProductsPageViewModel>(),
                NavigationNames.ActualDiscounts => CreateContent<ActualDiscountsPage, ActualDiscountsPageViewModel>(),
                NavigationNames.MyFilters => CreateContent<MyFiltersPage, MyFiltersPageViewModel>(),
                NavigationNames.Settings => CreateContent<SettingsPage, SettingsPageViewModel>(),
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
