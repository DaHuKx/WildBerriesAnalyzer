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
using WildBerriesAnalyzer.Mobile.Logging;
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
        private readonly IAdultContentPreferenceService _adultContentPreference;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _bagImagesCts;

        private WbFilter? _currentFilter;
        private bool _isBusy = true;
        private bool _isAddingToBag;
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
        private FilterTypeOption? _selectedFilterType;
        private FilterSettingsSection _selectedSection = FilterSettingsSection.Main;
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
            IFilterPresetBridge presetBridge,
            IAdultContentPreferenceService adultContentPreference)
        {
            _filtersService = filtersService;
            _authSessionService = authSessionService;
            _appThemeService = appThemeService;
            _productImageCache = productImageCache;
            _navigationService = navigationService;
            _presetBridge = presetBridge;
            _adultContentPreference = adultContentPreference;
            _presetBridge.OnPresetChosen = preset =>
                MainThread.BeginInvokeOnMainThread(() => ApplyPreset(preset));
            _adultContentPreference.Changed += (_, _) => ApplyAdultContentPreferenceToBag();

            FilterSettingsTabs =
            [
                new FilterSettingsTab(FilterSettingsSection.Templates, "Шаблоны"),
                new FilterSettingsTab(FilterSettingsSection.Main, "Основные"),
                new FilterSettingsTab(FilterSettingsSection.Strategies, "Стратегии"),
                new FilterSettingsTab(FilterSettingsSection.Markets, "Магазины"),
                new FilterSettingsTab(FilterSettingsSection.Bag, "Корзина"),
                new FilterSettingsTab(FilterSettingsSection.Categories, "Категории")
            ];
            SyncFilterSettingsTabs();

            Templates = new ObservableCollection<FilterPreset>(FilterPresetsCatalog.All);

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

            foreach (MarketType marketType in Enum.GetValues(typeof(MarketType)))
            {
                MarketTypeOptions.Add(new MarketTypeOption(
                    marketType,
                    ToMarketTypeText(marketType),
                    false));
            }

            RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);
            OpenPresetsCommand = new DelegateCommand(async () => await OpenPresetsAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            ApplyTemplateCommand = new DelegateCommand<FilterPreset>(ApplyTemplate, _ => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            SelectFilterSettingsTabCommand = new DelegateCommand<FilterSettingsTab>(SelectFilterSettingsTab);
            SaveFilterCommand = new DelegateCommand(async () => await SaveFilterAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
            AddArticlesCommand = new DelegateCommand(async () => await AddArticlesAsync(), () => !IsBusy && IsLoaded)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoaded);
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
        /// После share из WB/Ozon сразу открываем раздел корзины.
        /// </summary>
        public void ShowOwnBagSection()
        {
            var ownBag = FilterTypes.FirstOrDefault(x => x.Type == ProductsFilterType.OwnBag);
            if (ownBag is not null)
            {
                SelectedFilterType = ownBag;
            }

            SelectedSection = FilterSettingsSection.Bag;
        }

        public string Title => "Настройки фильтров";

        public string Subtitle => "Разделы настроек — сверху. Сохраните изменения перед выходом.";

        public string BrandLabel => "PRICELAB";

        public List<FilterSettingsTab> FilterSettingsTabs { get; }

        public ObservableCollection<FilterPreset> Templates { get; }

        public FilterSettingsSection SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (SetProperty(ref _selectedSection, value))
                {
                    SyncFilterSettingsTabs();
                    RaisePropertyChanged(nameof(IsTemplatesSection));
                    RaisePropertyChanged(nameof(IsMainSection));
                    RaisePropertyChanged(nameof(IsStrategiesSection));
                    RaisePropertyChanged(nameof(IsMarketsSection));
                    RaisePropertyChanged(nameof(IsBagSection));
                    RaisePropertyChanged(nameof(IsCategoriesSection));
                    RaisePropertyChanged(nameof(CurrentSectionTitle));
                }
            }
        }

        public bool IsTemplatesSection => SelectedSection == FilterSettingsSection.Templates;

        public bool IsMainSection => SelectedSection == FilterSettingsSection.Main;

        public bool IsStrategiesSection => SelectedSection == FilterSettingsSection.Strategies;

        public bool IsMarketsSection => SelectedSection == FilterSettingsSection.Markets;

        public bool IsBagSection => SelectedSection == FilterSettingsSection.Bag;

        public bool IsCategoriesSection => SelectedSection == FilterSettingsSection.Categories;

        public string CurrentSectionTitle => SelectedSection switch
        {
            FilterSettingsSection.Templates => "Шаблоны",
            FilterSettingsSection.Main => "Основные",
            FilterSettingsSection.Strategies => "Стратегии",
            FilterSettingsSection.Markets => "Магазины",
            FilterSettingsSection.Bag => "Корзина",
            FilterSettingsSection.Categories => "Категории",
            _ => Title
        };

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

        public bool IsAddingToBag
        {
            get => _isAddingToBag;
            private set => SetProperty(ref _isAddingToBag, value);
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
                    RaisePropertyChanged(nameof(ShowNonBagConditions));
                    RaisePropertyChanged(nameof(IsCategoryFilter));
                }
            }
        }

        public bool IsOwnBagFilter => SelectedFilterType?.Type == ProductsFilterType.OwnBag;

        public bool ShowNonBagConditions => !IsOwnBagFilter;

        public bool IsCategoryFilter =>
            SelectedFilterType?.Type is ProductsFilterType.Categories_BlackList or ProductsFilterType.Categories_WhiteList;

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

        public bool HasCategoryOptions => CategoryOptions.Count > 0;

        public bool HasNoCategoryOptions => CategoryOptions.Count == 0;

        public List<BagSortOption> BagSortOptions { get; }

        public ObservableCollection<string> BagBrandOptions { get; } = new();

        public ObservableCollection<StrategyOption> StrategyOptions { get; } = new();

        public ObservableCollection<MarketTypeOption> MarketTypeOptions { get; } = new();

        public ObservableCollection<CategoryListOption> CategoryOptions { get; } = new();

        public ObservableCollection<BagProductItem> BagProducts { get; } = new();

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand OpenPresetsCommand { get; }

        public DelegateCommand<FilterPreset> ApplyTemplateCommand { get; }

        public DelegateCommand<FilterSettingsTab> SelectFilterSettingsTabCommand { get; }

        public DelegateCommand SaveFilterCommand { get; }

        public DelegateCommand AddArticlesCommand { get; }

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

            foreach (var option in MarketTypeOptions)
            {
                option.RefreshThemeColors();
            }

            foreach (var option in CategoryOptions)
            {
                option.RefreshThemeColors();
            }

            foreach (var tab in FilterSettingsTabs)
            {
                tab.RefreshThemeColors();
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

        private void SelectFilterSettingsTab(FilterSettingsTab? tab)
        {
            if (tab is null)
            {
                return;
            }

            SelectedSection = tab.Section;
        }

        private void SyncFilterSettingsTabs()
        {
            foreach (var tab in FilterSettingsTabs)
            {
                tab.IsSelected = tab.Section == SelectedSection;
            }
        }

        private void ApplyTemplate(FilterPreset? preset)
        {
            if (preset is null)
            {
                return;
            }

            ApplyPreset(preset);
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

                AppLog.Action("MyFilters", "Load");

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

                var knownCategories = await Task.Run(async () =>
                {
                    return await _filtersService.GetKnownCategoriesAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

                // Тяжёлую подготовку списков — тоже вне UI.
                var bagItems = await Task.Run(() => BuildBagProductItems(data.BagProducts)).ConfigureAwait(false);
                var categoryOptions = await Task.Run(() =>
                        BuildCategoryListOptions(knownCategories, data.Categories))
                    .ConfigureAwait(false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _currentFilter = data.Filter;
                    _allFilterCategories = data.Categories;
                    ApplyFilter(data.Filter);
                    ApplyPreparedBagProducts(bagItems);
                    ApplyCategoryOptions(categoryOptions);
                    IsLoaded = true;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "MyFilters", "Load");
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
                AppLog.Action("MyFilters", "Save");

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

                var selectedMarkets = MarketTypeOptions
                    .Where(m => m.IsSelected)
                    .Select(m => m.MarketType)
                    .ToList();

                _currentFilter.DiscontMinPercent = minPercent;
                _currentFilter.MinReviewsCount = minReviews;
                _currentFilter.MinRating = minRating;
                _currentFilter.ProductsFilterType = SelectedFilterType.Type;
                _currentFilter.ReferencePriceStrartegies = selectedStrategies.Count > 0
                    ? selectedStrategies
                    : null;
                _currentFilter.MarketTypes = selectedMarkets.Count > 0
                    ? selectedMarkets
                    : null;

                await _filtersService.UpdateFilterAsync(_currentFilter);
                await SyncFilterCategoriesAsync(user.Id);
                StatusMessage = "Фильтры сохранены.";
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "MyFilters", "Save");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SyncFilterCategoriesAsync(int userId)
        {
            var existing = await _filtersService.GetFilterCategoriesAsync(userId);
            var desired = CategoryOptions
                .Where(o => o.Selection != CategoryListSelection.None)
                .Select(o => (
                    CategoryId: o.CategoryId,
                    Type: ToCategoryFilterType(o.Selection)!.Value))
                .ToList();

            foreach (var row in existing)
            {
                var keep = desired.Any(d => d.CategoryId == row.CategoryId && d.Type == row.Type);
                if (!keep)
                {
                    await _filtersService.RemoveFilterCategoryAsync(userId, row.Id);
                }
            }

            var remaining = await _filtersService.GetFilterCategoriesAsync(userId);
            foreach (var item in desired)
            {
                var already = remaining.Any(r => r.CategoryId == item.CategoryId && r.Type == item.Type);
                if (!already)
                {
                    await _filtersService.AddFilterCategoryAsync(userId, item.CategoryId, item.Type);
                }
            }

            _allFilterCategories = await _filtersService.GetFilterCategoriesAsync(userId);
        }

        private static CategoryFilterType? ToCategoryFilterType(CategoryListSelection selection) => selection switch
        {
            CategoryListSelection.WhiteList => CategoryFilterType.WhiteList,
            CategoryListSelection.BlackList => CategoryFilterType.BlackList,
            _ => null
        };

        private async Task AddArticlesAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;
                AppLog.Action("MyFilters", "AddArticles");

                var user = _authSessionService.CurrentUser;
                if (user is null || user.Id <= 0)
                {
                    ErrorMessage = "Пользователь не авторизован.";
                    return;
                }

                var input = NewArticleText?.Trim() ?? string.Empty;
                if (input.Length == 0)
                {
                    ErrorMessage = "Укажите артикулы или ссылку на общую корзину WB/Ozon.";
                    return;
                }

                IsAddingToBag = true;
                AddBagProductsResult result;
                if (ProductHelper.TryExtractBasketShareId(input, out _) ||
                    ProductHelper.TryExtractOzonCartShareId(input, out _))
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
                AppLog.Error(ex, "MyFilters", "AddArticles", "validation");
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "MyFilters", "AddArticles");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsAddingToBag = false;
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
                AppLog.Action("MyFilters", "RemoveBagProduct", $"id={item.ProductId}");

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
                AppLog.Error(ex, "MyFilters", "RemoveBagProduct");
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
                AppLog.Action("MyFilters", "ClearBag", $"count={_allBagProducts.Count}");

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
                AppLog.Error(ex, "MyFilters", "ClearBag");
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
            AppLog.Action("MyFilters", "ClearBagFilters");
            _bagSearchText = string.Empty;
            RaisePropertyChanged(nameof(BagSearchText));
            _selectedBagBrand = AllBrandsLabel;
            RaisePropertyChanged(nameof(SelectedBagBrand));
            _selectedBagSort = BagSortOptions[0];
            RaisePropertyChanged(nameof(SelectedBagSort));
            RaisePropertyChanged(nameof(HasActiveBagFilters));
            RefreshVisibleBagProducts();
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

            var selectedMarkets = filter.MarketTypes?.ToHashSet() ?? [];
            foreach (var option in MarketTypeOptions)
            {
                option.IsSelected = selectedMarkets.Contains(option.MarketType);
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

            foreach (var option in MarketTypeOptions)
            {
                option.IsSelected = true;
            }

            ErrorMessage = string.Empty;
            StatusMessage = $"Пресет «{preset.Title}» применён. Нажмите «Сохранить», чтобы зафиксировать.";
            SelectedSection = FilterSettingsSection.Main;
        }

        private void ApplyBagProducts(List<WbProduct> products)
        {
            ApplyPreparedBagProducts(BuildBagProductItems(products));
        }

        private List<BagProductItem> BuildBagProductItems(List<WbProduct> products)
        {
            var showAdult = _adultContentPreference.ShowAdultContent;
            return products.Select(product =>
            {
                string? sizeImageUrl = null;
                if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                {
                    sizeImageUrl = product.ImageUrl;
                }

                var item = new BagProductItem
                {
                    ProductId = product.Id,
                    MarketType = product.MarketType,
                    Name = string.IsNullOrWhiteSpace(product.Name) ? "Без названия" : product.Name,
                    Brand = product.Brand ?? string.Empty,
                    Article = product.IdInMarket.ToString(),
                    IsAdult = product.IsAdult,
                    ImageUrl = product.ImageUrl,
                    SizeImageUrl = sizeImageUrl ?? product.ImageUrl
                };
                item.ApplyShowAdultContent(showAdult);
                return item;
            }).ToList();
        }

        private void ApplyAdultContentPreferenceToBag()
        {
            var show = _adultContentPreference.ShowAdultContent;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in _allBagProducts)
                {
                    item.ApplyShowAdultContent(show);
                }
            });
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
            !IsBusy && !IsLoadingMoreBag && HasMoreBagItems && IsBagSection;

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

        private static List<CategoryListOption> BuildCategoryListOptions(
            List<WbCategory> knownCategories,
            List<WbFilterCategory> filterCategories)
        {
            var selectionByCategoryId = filterCategories
                .GroupBy(c => c.CategoryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Type switch
                    {
                        CategoryFilterType.WhiteList => CategoryListSelection.WhiteList,
                        CategoryFilterType.BlackList => CategoryListSelection.BlackList,
                        _ => CategoryListSelection.None
                    });

            return knownCategories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c =>
                {
                    selectionByCategoryId.TryGetValue(c.Id, out var selection);
                    return new CategoryListOption(c.Id, c.Name, selection);
                })
                .ToList();
        }

        private void ApplyCategoryOptions(List<CategoryListOption> options)
        {
            CategoryOptions.Clear();
            foreach (var option in options)
            {
                CategoryOptions.Add(option);
            }

            RaisePropertyChanged(nameof(HasCategoryOptions));
            RaisePropertyChanged(nameof(HasNoCategoryOptions));
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

        private static string ToMarketTypeText(MarketType marketType) => marketType switch
        {
            MarketType.Ozon => "Ozon",
            _ => "Wildberries"
        };
    }
}
