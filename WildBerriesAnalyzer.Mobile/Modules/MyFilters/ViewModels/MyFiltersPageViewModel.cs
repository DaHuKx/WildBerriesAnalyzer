using System.Collections.ObjectModel;
using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.Modules.MyFilters.Helpers;
using WildBerriesAnalyzer.Modules.MyFilters.Models;
using WildBerriesAnalyzer.Modules.MyFilters.Services;

namespace WildBerriesAnalyzer.Modules.MyFilters.ViewModels
{
    public class MyFiltersPageViewModel : BindableBase
    {
        private const int BagPageSize = 20;
        private const string AllBrandsLabel = "Все бренды";

        private readonly IFiltersService _filtersService;
        private readonly IAuthSessionService _authSessionService;
        private readonly IAppThemeService _appThemeService;
        private readonly IProductImageCache _productImageCache;
        private readonly INavigationService _navigationService;
        private readonly IFilterPresetBridge _presetBridge;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _bagImagesCts;

        private WbFilter? _currentFilter;
        private bool _isBusy = true;
        private bool _isLoaded;
        private bool _loadStarted;
        private bool _isLoadingMoreBag;
        private bool _hasMoreBagItems;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private string _discontMinPercentText = "1";
        private string _minReviewsCountText = "0";
        private string _minRatingText = "0";
        private string _newArticleText = string.Empty;
        private string _newCategoryIdText = string.Empty;
        private FilterTypeOption? _selectedFilterType;
        private int _bagProductsCount;
        private string _bagSearchText = string.Empty;
        private string _selectedBagBrand = AllBrandsLabel;
        private BagSortOption? _selectedBagSort;
        private bool _isBagFiltersExpanded;
        private bool _isStrategyHelpVisible;
        private string _strategyHelpTitle = string.Empty;
        private string _strategyHelpDescription = string.Empty;
        private string _strategyHelpExample = string.Empty;
        private List<WbFilterCategory> _allFilterCategories = [];
        private List<BagProductItem> _allBagProducts = [];
        private List<BagProductItem> _pipelineBagProducts = [];
        private int _visibleBagCount;

        public MyFiltersPageViewModel(
            IFiltersService filtersService,
            IAuthSessionService authSessionService,
            IAppThemeService appThemeService,
            IProductImageCache productImageCache,
            INavigationService navigationService,
            IFilterPresetBridge presetBridge)
        {
            _filtersService = filtersService;
            _authSessionService = authSessionService;
            _appThemeService = appThemeService;
            _productImageCache = productImageCache;
            _navigationService = navigationService;
            _presetBridge = presetBridge;
            _presetBridge.OnPresetChosen = preset =>
                MainThread.BeginInvokeOnMainThread(() => ApplyPreset(preset));

            FilterTypes =
            [
                new FilterTypeOption(
                    ProductsFilterType.None,
                    "Все товары",
                    "Без ограничений",
                    "⧉",
                    gridRow: 0,
                    gridColumn: 0),
                new FilterTypeOption(
                    ProductsFilterType.OwnBag,
                    "Моя корзина",
                    "Только добавленные",
                    "▣",
                    gridRow: 0,
                    gridColumn: 1),
                new FilterTypeOption(
                    ProductsFilterType.Categories_BlackList,
                    "Исключить",
                    "Чёрный список",
                    "⊠",
                    gridRow: 1,
                    gridColumn: 0),
                new FilterTypeOption(
                    ProductsFilterType.Categories_WhiteList,
                    "Только",
                    "Белый список",
                    "▥",
                    gridRow: 1,
                    gridColumn: 1)
            ];

            BagSortOptions =
            [
                new BagSortOption(BagSortMode.NameAsc, "Название А–Я"),
                new BagSortOption(BagSortMode.NameDesc, "Название Я–А"),
                new BagSortOption(BagSortMode.ArticleAsc, "Артикул ↑"),
                new BagSortOption(BagSortMode.ArticleDesc, "Артикул ↓"),
                new BagSortOption(BagSortMode.BrandAsc, "Бренд А–Я"),
                new BagSortOption(BagSortMode.BrandDesc, "Бренд Я–А")
            ];
            _selectedBagSort = BagSortOptions[0];

            BagBrandOptions.Add(AllBrandsLabel);

            foreach (ReferencePriceStrategy strategy in Enum.GetValues(typeof(ReferencePriceStrategy)))
            {
                StrategyOptions.Add(new StrategyOption(
                    strategy,
                    ToStrategyText(strategy),
                    false,
                    ShowStrategyHelp));
            }

            RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);
            OpenPresetsCommand = new DelegateCommand(async () => await OpenPresetsAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            SaveFilterCommand = new DelegateCommand(async () => await SaveFilterAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            AddArticlesCommand = new DelegateCommand(async () => await AddArticlesAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            AddCategoryCommand = new DelegateCommand(async () => await AddCategoryAsync(), () => !IsBusy && IsLoaded && IsCategoryFilter)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded)
                .ObservesProperty(() => IsCategoryFilter);
            ClearBagFiltersCommand = new DelegateCommand(ClearBagFilters, () => HasActiveBagFilters)
                .ObservesProperty(() => HasActiveBagFilters);
            ClearBagCommand = new DelegateCommand(async () => await ClearBagAsync(), () => !IsBusy && IsLoaded && HasBagProducts)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded)
                .ObservesProperty(() => HasBagProducts);
            ToggleBagFiltersCommand = new DelegateCommand(() => IsBagFiltersExpanded = !IsBagFiltersExpanded);
            LoadMoreBagCommand = new DelegateCommand(async () => await LoadMoreBagAsync(), CanLoadMoreBag)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoadingMoreBag)
                .ObservesProperty(() => HasMoreBagItems);
            SelectFilterTypeCommand = new DelegateCommand<FilterTypeOption>(SelectFilterType);
            DismissSnackbarCommand = new DelegateCommand(DismissSnackbar);
            DismissStrategyHelpCommand = new DelegateCommand(DismissStrategyHelp);

            _appThemeService.ThemeChanged += (_, _) => RefreshThemeDependentUi();
            // Загрузка стартует из View.Loaded — после первой отрисовки UI.
        }

        /// <summary>
        /// Запускает загрузку один раз, когда страница уже показана (loader может крутиться).
        /// </summary>
        public Task LoadIfNeededAsync()
        {
            if (_loadStarted || _isLoaded)
            {
                return Task.CompletedTask;
            }

            _loadStarted = true;
            return LoadAsync();
        }

        /// <summary>
        /// После share из WB сразу открываем вкладку «Моя корзина».
        /// </summary>
        public void ShowOwnBagSection()
        {
            var ownBag = FilterTypes.FirstOrDefault(x => x.Type == ProductsFilterType.OwnBag);
            if (ownBag is null)
            {
                return;
            }

            SelectedFilterType = ownBag;
        }

        public string Title => "Настройки фильтров";

        public string Subtitle => "Настройте условия поиска подходящих скидок";

        public string BrandLabel => "PRICELAB";

        /// <summary>
        /// Первичная загрузка страницы (не сохранение): показываем loader под заголовком.
        /// </summary>
        public bool IsPageLoading => IsBusy && !IsLoaded;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(IsPageLoading));
                }
            }
        }

        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                if (SetProperty(ref _isLoaded, value))
                {
                    RaisePropertyChanged(nameof(IsPageLoading));
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
                    RaisePropertyChanged(nameof(HasError));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ShowSnackbar(value, isError: true);
                    }
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    RaisePropertyChanged(nameof(HasStatus));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ShowSnackbar(value, isError: false);
                    }
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

        public string SnackbarMessage
        {
            get => _snackbarMessage;
            private set => SetProperty(ref _snackbarMessage, value);
        }

        public bool IsSnackbarVisible
        {
            get => _isSnackbarVisible;
            private set => SetProperty(ref _isSnackbarVisible, value);
        }

        public bool IsSnackbarError
        {
            get => _isSnackbarError;
            private set
            {
                if (SetProperty(ref _isSnackbarError, value))
                {
                    RaisePropertyChanged(nameof(SnackbarBackground));
                }
            }
        }

        public Color SnackbarBackground =>
            IsSnackbarError ? ThemeColors.Error : ThemeColors.Success;

        public string DiscontMinPercentText
        {
            get => _discontMinPercentText;
            set => SetProperty(ref _discontMinPercentText, value);
        }

        public string MinReviewsCountText
        {
            get => _minReviewsCountText;
            set => SetProperty(ref _minReviewsCountText, value);
        }

        public string MinRatingText
        {
            get => _minRatingText;
            set => SetProperty(ref _minRatingText, value);
        }

        public string NewArticleText
        {
            get => _newArticleText;
            set => SetProperty(ref _newArticleText, value);
        }

        public string NewCategoryIdText
        {
            get => _newCategoryIdText;
            set => SetProperty(ref _newCategoryIdText, value);
        }

        public List<FilterTypeOption> FilterTypes { get; }

        public FilterTypeOption? SelectedFilterType
        {
            get => _selectedFilterType;
            set
            {
                if (SetProperty(ref _selectedFilterType, value))
                {
                    SyncFilterTypeSelection();
                    RaisePropertyChanged(nameof(IsOwnBagFilter));
                    RaisePropertyChanged(nameof(IsCategoryFilter));
                    RaisePropertyChanged(nameof(CategoriesSectionTitle));
                    ApplyCategories(_allFilterCategories);
                }
            }
        }

        public bool IsOwnBagFilter => SelectedFilterType?.Type == ProductsFilterType.OwnBag;

        public bool IsCategoryFilter =>
            SelectedFilterType?.Type is ProductsFilterType.Categories_BlackList or ProductsFilterType.Categories_WhiteList;

        public string CategoriesSectionTitle => SelectedFilterType?.Type switch
        {
            ProductsFilterType.Categories_BlackList => "Чёрный список категорий",
            ProductsFilterType.Categories_WhiteList => "Белый список категорий",
            _ => "Категории"
        };

        public int BagProductsCount
        {
            get => _bagProductsCount;
            set => SetProperty(ref _bagProductsCount, value);
        }

        public string BagSearchText
        {
            get => _bagSearchText;
            set
            {
                if (SetProperty(ref _bagSearchText, value))
                {
                    RaisePropertyChanged(nameof(HasActiveBagFilters));
                    RefreshVisibleBagProducts();
                }
            }
        }

        public string SelectedBagBrand
        {
            get => _selectedBagBrand;
            set
            {
                if (SetProperty(ref _selectedBagBrand, value ?? AllBrandsLabel))
                {
                    RaisePropertyChanged(nameof(HasActiveBagFilters));
                    RefreshVisibleBagProducts();
                }
            }
        }

        public BagSortOption? SelectedBagSort
        {
            get => _selectedBagSort;
            set
            {
                if (SetProperty(ref _selectedBagSort, value))
                {
                    RaisePropertyChanged(nameof(HasActiveBagFilters));
                    RefreshVisibleBagProducts();
                }
            }
        }

        public bool HasBagProducts => _allBagProducts.Count > 0;

        public bool HasNoBagProducts => _allBagProducts.Count == 0;

        public bool HasVisibleBagProducts => BagProducts.Count > 0;

        public bool HasNoVisibleBagProducts => HasBagProducts && _pipelineBagProducts.Count == 0;

        public bool IsLoadingMoreBag
        {
            get => _isLoadingMoreBag;
            private set => SetProperty(ref _isLoadingMoreBag, value);
        }

        public bool HasMoreBagItems
        {
            get => _hasMoreBagItems;
            private set => SetProperty(ref _hasMoreBagItems, value);
        }

        public bool HasActiveBagFilters =>
            !string.IsNullOrWhiteSpace(BagSearchText) ||
            (!string.IsNullOrWhiteSpace(SelectedBagBrand) && SelectedBagBrand != AllBrandsLabel) ||
            SelectedBagSort?.Mode != BagSortMode.NameAsc;

        public bool IsBagFiltersExpanded
        {
            get => _isBagFiltersExpanded;
            set
            {
                if (SetProperty(ref _isBagFiltersExpanded, value))
                {
                    RaisePropertyChanged(nameof(BagFiltersExpandIcon));
                }
            }
        }

        public string BagFiltersExpandIcon => IsBagFiltersExpanded ? "▲" : "▼";

        public string BagVisibleCountText
        {
            get
            {
                var matched = _pipelineBagProducts.Count;
                var shown = BagProducts.Count;
                if (matched == 0)
                {
                    return BagProductsCount.ToString(CultureInfo.InvariantCulture);
                }

                if (shown < matched || matched != BagProductsCount)
                {
                    return $"{shown} из {matched}";
                }

                return BagProductsCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        public bool HasCategories => FilterCategories.Count > 0;

        public bool HasNoCategories => FilterCategories.Count == 0;

        public List<BagSortOption> BagSortOptions { get; }

        public ObservableCollection<string> BagBrandOptions { get; } = new();

        public ObservableCollection<StrategyOption> StrategyOptions { get; } = new();

        public ObservableCollection<BagProductItem> BagProducts { get; } = new();

        public ObservableCollection<FilterCategoryItem> FilterCategories { get; } = new();

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand OpenPresetsCommand { get; }

        public DelegateCommand SaveFilterCommand { get; }

        public DelegateCommand AddArticlesCommand { get; }

        public DelegateCommand AddCategoryCommand { get; }

        public DelegateCommand ClearBagFiltersCommand { get; }

        public DelegateCommand ClearBagCommand { get; }

        public DelegateCommand ToggleBagFiltersCommand { get; }

        public DelegateCommand LoadMoreBagCommand { get; }

        public DelegateCommand<FilterTypeOption> SelectFilterTypeCommand { get; }

        public DelegateCommand DismissSnackbarCommand { get; }

        public DelegateCommand DismissStrategyHelpCommand { get; }

        public bool IsStrategyHelpVisible
        {
            get => _isStrategyHelpVisible;
            private set => SetProperty(ref _isStrategyHelpVisible, value);
        }

        public string StrategyHelpTitle
        {
            get => _strategyHelpTitle;
            private set => SetProperty(ref _strategyHelpTitle, value);
        }

        public string StrategyHelpDescription
        {
            get => _strategyHelpDescription;
            private set => SetProperty(ref _strategyHelpDescription, value);
        }

        public string StrategyHelpExample
        {
            get => _strategyHelpExample;
            private set => SetProperty(ref _strategyHelpExample, value);
        }

        private void RefreshThemeDependentUi()
        {
            foreach (var option in FilterTypes)
            {
                option.RefreshThemeColors();
            }

            foreach (var option in StrategyOptions)
            {
                option.RefreshThemeColors();
            }

            RaisePropertyChanged(nameof(SnackbarBackground));
        }

        private void ShowSnackbar(string message, bool isError)
        {
            _snackbarCts?.Cancel();
            _snackbarCts?.Dispose();
            _snackbarCts = new CancellationTokenSource();
            var token = _snackbarCts.Token;

            SnackbarMessage = message.Trim();
            IsSnackbarError = isError;
            IsSnackbarVisible = true;

            _ = DismissSnackbarAfterDelayAsync(token);
        }

        private async Task DismissSnackbarAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), token);
                if (!token.IsCancellationRequested)
                {
                    DismissSnackbar();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void DismissSnackbar()
        {
            IsSnackbarVisible = false;
        }

        private void ShowStrategyHelp(StrategyOption option)
        {
            var help = StrategyHelpCatalog.Get(option.Strategy);
            StrategyHelpTitle = help.Title;
            StrategyHelpDescription = help.Description;
            StrategyHelpExample = help.Example;
            IsStrategyHelpVisible = true;
        }

        private void DismissStrategyHelp()
        {
            IsStrategyHelpVisible = false;
        }

        private void SelectFilterType(FilterTypeOption? option)
        {
            if (option is null || IsBusy)
            {
                return;
            }

            SelectedFilterType = option;
        }

        private void SyncFilterTypeSelection()
        {
            foreach (var option in FilterTypes)
            {
                option.IsSelected = option.Type == SelectedFilterType?.Type;
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = true;
                    ErrorMessage = string.Empty;
                    StatusMessage = string.Empty;
                    IsLoaded = false;
                }).ConfigureAwait(false);

                // Пауза: дать кадр на отрисовку loader до сетевого ожидания.
                await Task.Delay(16).ConfigureAwait(false);

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ErrorMessage = "Пользователь не авторизован.";
                    }).ConfigureAwait(false);
                    return;
                }

                var userId = user.Id;

                // HTTP + JSON полностью на пуле потоков (не UI).
                var data = await Task.Run(async () =>
                {
                    return await _filtersService.GetUserFilterDataAsync(userId).ConfigureAwait(false);
                }).ConfigureAwait(false);

                // Тяжёлую подготовку списков — тоже вне UI.
                var bagItems = await Task.Run(() => BuildBagProductItems(data.BagProducts)).ConfigureAwait(false);
                var categoryItems = await Task.Run(() =>
                        BuildCategoryItems(data.Categories, data.Filter.ProductsFilterType))
                    .ConfigureAwait(false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _currentFilter = data.Filter;
                    _allFilterCategories = data.Categories;
                    ApplyFilter(data.Filter);
                    ApplyPreparedBagProducts(bagItems);
                    ApplyPreparedCategories(categoryItems);
                    IsLoaded = true;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ErrorMessage = ex.Message;
                    IsLoaded = false;
                    _loadStarted = false;
                }).ConfigureAwait(false);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false).ConfigureAwait(false);
            }
        }

        private async Task SaveFilterAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0 || _currentFilter is null)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                if (!int.TryParse(DiscontMinPercentText.Trim(), out var minPercent) || minPercent < 1 || minPercent > 100)
                {
                    ErrorMessage = "Мин. скидка должна быть числом от 1 до 100.";
                    return;
                }

                if (!int.TryParse(MinReviewsCountText.Trim(), out var minReviews) || minReviews < 0)
                {
                    ErrorMessage = "Мин. отзывов должно быть неотрицательным числом.";
                    return;
                }

                if (!float.TryParse(MinRatingText.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var minRating) ||
                    minRating < 0 || minRating > 5)
                {
                    ErrorMessage = "Мин. рейтинг должен быть числом от 0 до 5.";
                    return;
                }

                if (SelectedFilterType is null)
                {
                    ErrorMessage = "Выберите тип фильтрации товаров.";
                    return;
                }

                // Ничего не выбрано = все стратегии (null на сервере).
                var selectedStrategies = StrategyOptions
                    .Where(s => s.IsSelected)
                    .Select(s => s.Strategy)
                    .ToList();

                _currentFilter.DiscontMinPercent = minPercent;
                _currentFilter.MinReviewsCount = minReviews;
                _currentFilter.MinRating = minRating;
                _currentFilter.ProductsFilterType = SelectedFilterType.Type;
                _currentFilter.ReferencePriceStrartegies = selectedStrategies.Count > 0
                    ? selectedStrategies
                    : null;

                await _filtersService.UpdateFilterAsync(_currentFilter);
                StatusMessage = "Фильтры сохранены.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddArticlesAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                var input = NewArticleText?.Trim() ?? string.Empty;
                if (input.Length == 0)
                {
                    ErrorMessage = "Укажите артикулы или ссылку на общую корзину WB.";
                    return;
                }

                AddBagProductsResult result;
                if (ProductHelper.TryExtractBasketShareId(input, out _))
                {
                    result = await _filtersService.AddProductsToBagFromBasketShareAsync(user.Id, input);
                }
                else
                {
                    var rawIds = input.Split([' ', '\n', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                    result = await _filtersService.AddProductsToBagAsync(user.Id, rawIds);
                }

                NewArticleText = string.Empty;
                ApplyBagProducts(result.BagProducts);

                StatusMessage = result.AddedProducts.Count > 0
                    ? $"Добавлено в корзину: {result.AddedProducts.Count}."
                    : "Новых товаров не добавлено (уже в корзине).";
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RemoveBagProductAsync(BagProductItem item)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                await _filtersService.RemoveProductsFromBagAsync(user.Id, [item.ProductId]);
                _allBagProducts.RemoveAll(p => p.ProductId == item.ProductId);
                RebuildBagBrandOptions();
                RefreshVisibleBagProducts();
                StatusMessage = "Товар удалён из корзины.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ClearBagAsync()
        {
            if (_allBagProducts.Count == 0)
            {
                return;
            }

            var confirmed = await ConfirmAsync(
                "Очистить корзину?",
                $"Будут удалены все товары из корзины ({_allBagProducts.Count}). Это действие нельзя отменить.",
                "Очистить",
                "Отмена");

            if (!confirmed)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                var productIds = _allBagProducts.Select(p => p.ProductId).ToList();
                await _filtersService.RemoveProductsFromBagAsync(user.Id, productIds);

                _allBagProducts = [];
                RebuildBagBrandOptions();
                RefreshVisibleBagProducts();
                StatusMessage = "Корзина очищена.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static async Task<bool> ConfirmAsync(
            string title,
            string message,
            string accept,
            string cancel)
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null)
            {
                return false;
            }

            return await MainThread.InvokeOnMainThreadAsync(() =>
                page.DisplayAlert(title, message, accept, cancel));
        }

        private void ClearBagFilters()
        {
            _bagSearchText = string.Empty;
            RaisePropertyChanged(nameof(BagSearchText));
            _selectedBagBrand = AllBrandsLabel;
            RaisePropertyChanged(nameof(SelectedBagBrand));
            _selectedBagSort = BagSortOptions[0];
            RaisePropertyChanged(nameof(SelectedBagSort));
            RaisePropertyChanged(nameof(HasActiveBagFilters));
            RefreshVisibleBagProducts();
        }

        private async Task AddCategoryAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0 || SelectedFilterType is null)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                if (!int.TryParse(NewCategoryIdText.Trim(), out var categoryId) || categoryId <= 0)
                {
                    ErrorMessage = "Укажите корректный ID категории.";
                    return;
                }

                var type = SelectedFilterType.Type == ProductsFilterType.Categories_WhiteList
                    ? CategoryFilterType.WhiteList
                    : CategoryFilterType.BlackList;

                await _filtersService.AddFilterCategoryAsync(user.Id, categoryId, type);
                NewCategoryIdText = string.Empty;

                var categories = await _filtersService.GetFilterCategoriesAsync(user.Id);
                ApplyCategories(categories);
                StatusMessage = "Категория добавлена.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RemoveCategoryAsync(FilterCategoryItem item)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                await _filtersService.RemoveFilterCategoryAsync(user.Id, item.Id);
                _allFilterCategories.RemoveAll(c => c.Id == item.Id);
                FilterCategories.Remove(item);
                RaisePropertyChanged(nameof(HasCategories));
                RaisePropertyChanged(nameof(HasNoCategories));
                StatusMessage = "Категория удалена.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter(WbFilter filter)
        {
            DiscontMinPercentText = filter.DiscontMinPercent.ToString(CultureInfo.InvariantCulture);
            MinReviewsCountText = filter.MinReviewsCount.ToString(CultureInfo.InvariantCulture);
            MinRatingText = filter.MinRating.ToString("0.##", CultureInfo.InvariantCulture);
            SelectedFilterType = FilterTypes.First(t => t.Type == filter.ProductsFilterType);

            var selected = filter.ReferencePriceStrartegies?.ToHashSet() ?? [];
            foreach (var option in StrategyOptions)
            {
                option.IsSelected = selected.Contains(option.Strategy);
            }
        }

        private async Task OpenPresetsAsync()
        {
            await _navigationService.NavigateAsync(
                NavigationNames.FilterPresets,
                new NavigationParameters
                {
                    { KnownNavigationParameters.UseModalNavigation, true }
                });
        }

        private void ApplyPreset(FilterPreset preset)
        {
            ArgumentNullException.ThrowIfNull(preset);

            DiscontMinPercentText = preset.DiscontMinPercent.ToString(CultureInfo.InvariantCulture);
            MinReviewsCountText = preset.MinReviewsCount.ToString(CultureInfo.InvariantCulture);
            MinRatingText = preset.MinRating.ToString("0.##", CultureInfo.InvariantCulture);

            var type = FilterTypes.FirstOrDefault(t => t.Type == preset.ProductsFilterType)
                       ?? FilterTypes[0];
            SelectedFilterType = type;

            var selected = preset.Strategies is { Count: > 0 }
                ? preset.Strategies.ToHashSet()
                : null;

            foreach (var option in StrategyOptions)
            {
                option.IsSelected = selected is null || selected.Contains(option.Strategy);
            }

            ErrorMessage = string.Empty;
            StatusMessage = $"Пресет «{preset.Title}» применён. Нажмите «Сохранить», чтобы зафиксировать.";
        }

        private void ApplyBagProducts(List<WbProduct> products)
        {
            ApplyPreparedBagProducts(BuildBagProductItems(products));
        }

        private static List<BagProductItem> BuildBagProductItems(List<WbProduct> products)
        {
            return products.Select(product =>
            {
                string? sizeImageUrl = null;
                if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                {
                    sizeImageUrl = product.ImageUrl;
                }

                return new BagProductItem
                {
                    ProductId = product.Id,
                    Name = string.IsNullOrWhiteSpace(product.Name) ? "Без названия" : product.Name,
                    Brand = product.Brand ?? string.Empty,
                    Article = product.IdInMarket.ToString(),
                    ImageUrl = product.ImageUrl,
                    SizeImageUrl = sizeImageUrl ?? product.ImageUrl
                };
            }).ToList();
        }

        private void ApplyPreparedBagProducts(List<BagProductItem> items)
        {
            foreach (var item in items)
            {
                item.RemoveCommand = new DelegateCommand(async () => await RemoveBagProductAsync(item), () => !IsBusy)
                    .ObservesProperty(() => IsBusy);
            }

            _allBagProducts = items;
            RebuildBagBrandOptions();
            RefreshVisibleBagProducts();
        }

        private void RebuildBagBrandOptions()
        {
            var currentBrand = SelectedBagBrand;
            var brands = _allBagProducts
                .Select(p => p.Brand?.Trim() ?? string.Empty)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToList();

            BagBrandOptions.Clear();
            BagBrandOptions.Add(AllBrandsLabel);
            foreach (var brand in brands)
            {
                BagBrandOptions.Add(brand);
            }

            if (string.IsNullOrWhiteSpace(currentBrand) ||
                !BagBrandOptions.Contains(currentBrand, StringComparer.OrdinalIgnoreCase))
            {
                _selectedBagBrand = AllBrandsLabel;
                RaisePropertyChanged(nameof(SelectedBagBrand));
            }
        }

        private bool CanLoadMoreBag() =>
            !IsBusy && !IsLoadingMoreBag && HasMoreBagItems && IsOwnBagFilter;

        private async Task LoadMoreBagAsync()
        {
            if (!CanLoadMoreBag())
            {
                return;
            }

            try
            {
                IsLoadingMoreBag = true;
                await Task.Yield();
                AppendNextBagPage();
            }
            finally
            {
                IsLoadingMoreBag = false;
                RaisePropertyChanged(nameof(BagVisibleCountText));
            }
        }

        private void RefreshVisibleBagProducts()
        {
            _pipelineBagProducts = BuildBagPipeline().ToList();
            _visibleBagCount = 0;
            BagProducts.Clear();
            AppendNextBagPage();

            BagProductsCount = _allBagProducts.Count;
            RaisePropertyChanged(nameof(HasBagProducts));
            RaisePropertyChanged(nameof(HasNoBagProducts));
            RaisePropertyChanged(nameof(HasVisibleBagProducts));
            RaisePropertyChanged(nameof(HasNoVisibleBagProducts));
            RaisePropertyChanged(nameof(BagVisibleCountText));
            RaisePropertyChanged(nameof(HasActiveBagFilters));
        }

        private IEnumerable<BagProductItem> BuildBagPipeline()
        {
            IEnumerable<BagProductItem> query = _allBagProducts;

            var search = BagSearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    (p.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Brand?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Article?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(SelectedBagBrand) && SelectedBagBrand != AllBrandsLabel)
            {
                query = query.Where(p =>
                    string.Equals(p.Brand?.Trim(), SelectedBagBrand, StringComparison.OrdinalIgnoreCase));
            }

            return (SelectedBagSort?.Mode ?? BagSortMode.NameAsc) switch
            {
                BagSortMode.NameDesc => query.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase),
                BagSortMode.ArticleAsc => query.OrderBy(p => p.Article, StringComparer.OrdinalIgnoreCase),
                BagSortMode.ArticleDesc => query.OrderByDescending(p => p.Article, StringComparer.OrdinalIgnoreCase),
                BagSortMode.BrandAsc => query.OrderBy(p => p.Brand, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
                BagSortMode.BrandDesc => query.OrderByDescending(p => p.Brand, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
                _ => query.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            };
        }

        private void AppendNextBagPage()
        {
            if (_visibleBagCount >= _pipelineBagProducts.Count)
            {
                HasMoreBagItems = false;
                return;
            }

            var next = _pipelineBagProducts
                .Skip(_visibleBagCount)
                .Take(BagPageSize)
                .ToList();

            _visibleBagCount += next.Count;
            foreach (var item in next)
            {
                BagProducts.Add(item);
            }

            HasMoreBagItems = _visibleBagCount < _pipelineBagProducts.Count;
            RaisePropertyChanged(nameof(HasVisibleBagProducts));
            RaisePropertyChanged(nameof(BagVisibleCountText));

            if (next.Count > 0)
            {
                _ = PrefetchVisibleBagImagesAsync(next);
            }
        }

        private async Task PrefetchVisibleBagImagesAsync(IReadOnlyList<BagProductItem> items)
        {
            _bagImagesCts?.Cancel();
            _bagImagesCts?.Dispose();
            var cts = new CancellationTokenSource();
            _bagImagesCts = cts;
            var token = cts.Token;

            using var gate = new SemaphoreSlim(6);
            var tasks = items.Select(async item =>
            {
                try
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await item.LoadImageAsync(_productImageCache, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    gate.Release();
                }
            });

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ApplyCategories(List<WbFilterCategory> categories)
        {
            ApplyPreparedCategories(BuildCategoryItems(categories, SelectedFilterType?.Type));
        }

        private static List<FilterCategoryItem> BuildCategoryItems(
            List<WbFilterCategory> categories,
            ProductsFilterType? filterType)
        {
            CategoryFilterType? expectedType = filterType switch
            {
                ProductsFilterType.Categories_BlackList => CategoryFilterType.BlackList,
                ProductsFilterType.Categories_WhiteList => CategoryFilterType.WhiteList,
                _ => null
            };

            var visible = expectedType is null
                ? categories
                : categories.Where(c => c.Type == expectedType.Value).ToList();

            return visible.Select(category => new FilterCategoryItem
            {
                Id = category.Id,
                CategoryId = category.CategoryId,
                Name = category.Category?.Name ?? string.Empty
            }).ToList();
        }

        private void ApplyPreparedCategories(List<FilterCategoryItem> items)
        {
            // Сохраняем сырые категории из последнего ответа — для переключения black/white list.
            // Если вызываем после LoadAsync, _allFilterCategories уже должен быть задан в LoadAsync.
            FilterCategories.Clear();
            foreach (var item in items)
            {
                item.RemoveCommand = new DelegateCommand(async () => await RemoveCategoryAsync(item), () => !IsBusy)
                    .ObservesProperty(() => IsBusy);
                FilterCategories.Add(item);
            }

            RaisePropertyChanged(nameof(HasCategories));
            RaisePropertyChanged(nameof(HasNoCategories));
        }

        private static string ToStrategyText(ReferencePriceStrategy strategy) => strategy switch
        {
            ReferencePriceStrategy.LastKnownPrice => "Последняя известная цена",
            ReferencePriceStrategy.AveragePrice => "Средняя цена",
            ReferencePriceStrategy.Median => "Медианная цена",
            ReferencePriceStrategy.MinimumHistorical => "Минимальная за всё время",
            ReferencePriceStrategy.LowestPriceForLast30Days => "Минимальная за 30 дней",
            ReferencePriceStrategy.AveragePriceForLast30Days => "Средняя за 30 дней",
            ReferencePriceStrategy.MedianPriceForLast30Days => "Медианная за 30 дней",
            _ => strategy.ToString()
        };
    }
}
