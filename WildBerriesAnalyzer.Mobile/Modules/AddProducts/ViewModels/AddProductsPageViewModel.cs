using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.AddProducts.Models;
using WildBerriesAnalyzer.Modules.Products.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.AddProducts.ViewModels
{
    public class AddProductsPageViewModel : BindableBase, IDestructible
    {
        private const int PageSize = 15;
        private static readonly TimeSpan SearchHubConnectTimeout = TimeSpan.FromSeconds(3);
        private const string DefaultSearchProgressText = "Обработка запроса…";

        private readonly IProductsService _productsService;
        private readonly IProductImageCache _productImageCache;
        private readonly IAdultContentPreferenceService _adultContentPreference;
        private readonly ISearchHubClient _searchHubClient;

        private readonly List<ProductListItem> _pipelineProducts = [];

        private bool _isArticlesTab = true;
        private bool _isBusy;
        private bool _isSearchContinuing;
        private bool _isLoadingMore;
        private bool _hasMoreItems;
        private int _nameOpGeneration;
        private string _articlesText = string.Empty;
        private string _productNameText = string.Empty;
        private bool _searchWildberries = true;
        private bool _searchOzon = true;
        private ArticleMarketOption? _selectedArticleMarket;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _searchProgressText = DefaultSearchProgressText;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _imagesCts;

        public AddProductsPageViewModel(
            IProductsService productsService,
            IProductImageCache productImageCache,
            IAdultContentPreferenceService adultContentPreference,
            ISearchHubClient searchHubClient)
        {
            _productsService = productsService;
            _productImageCache = productImageCache;
            _adultContentPreference = adultContentPreference;
            _searchHubClient = searchHubClient;
            _adultContentPreference.Changed += (_, _) => ApplyAdultContentPreferenceToAll();
            _searchHubClient.ProgressReceived += OnSearchProgress;

            ArticleMarketOptions =
            [
                new ArticleMarketOption(MarketType.Wildberries, "Wildberries"),
                new ArticleMarketOption(MarketType.Ozon, "Ozon")
            ];
            _selectedArticleMarket = ArticleMarketOptions[0];

            ShowArticlesTabCommand = new DelegateCommand(() => SelectTab(articles: true), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            ShowNameTabCommand = new DelegateCommand(() => SelectTab(articles: false), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            AddByArticlesCommand = new DelegateCommand(async () => await AddByArticlesAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            SearchByNameCommand = new DelegateCommand(async () => await SearchByNameAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            AddByNameCommand = new DelegateCommand(async () => await AddByNameAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            LoadMoreCommand = new DelegateCommand(async () => await LoadMoreAsync(), CanLoadMore)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoadingMore)
                .ObservesProperty(() => HasMoreItems);

            OpenLinkCommand = new DelegateCommand<ProductListItem>(async item => await OpenLinkAsync(item));
            DismissSnackbarCommand = new DelegateCommand(DismissSnackbar);

            Results.CollectionChanged += (_, _) =>
            {
                RaisePropertyChanged(nameof(ShowEmptyMessage));
                RaisePropertyChanged(nameof(HasResults));
                RaisePropertyChanged(nameof(CountText));
                RaisePropertyChanged(nameof(IsInitialLoading));
            };
        }

        public string Title => "Добавление товаров";

        public string BrandLabel => "PRICELAB";

        public string Subtitle => "Добавляйте товары в каталог по артикулу или названию";

        public ObservableCollection<ProductListItem> Results { get; } = [];

        public IReadOnlyList<ArticleMarketOption> ArticleMarketOptions { get; }

        public DelegateCommand ShowArticlesTabCommand { get; }

        public DelegateCommand ShowNameTabCommand { get; }

        public DelegateCommand AddByArticlesCommand { get; }

        public DelegateCommand SearchByNameCommand { get; }

        public DelegateCommand AddByNameCommand { get; }

        public DelegateCommand LoadMoreCommand { get; }

        public DelegateCommand DismissSnackbarCommand { get; }

        public DelegateCommand<ProductListItem> OpenLinkCommand { get; }

        public bool IsArticlesTab
        {
            get => _isArticlesTab;
            private set
            {
                if (!SetProperty(ref _isArticlesTab, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(IsNameTab));
                RaisePropertyChanged(nameof(ArticlesTabBackground));
                RaisePropertyChanged(nameof(NameTabBackground));
                RaisePropertyChanged(nameof(ArticlesTabTextColor));
                RaisePropertyChanged(nameof(NameTabTextColor));
                RaisePropertyChanged(nameof(EmptyMessage));
            }
        }

        public bool IsNameTab => !IsArticlesTab;

        public Color ArticlesTabBackground =>
            IsArticlesTab ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color NameTabBackground =>
            IsNameTab ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color ArticlesTabTextColor =>
            IsArticlesTab ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public Color NameTabTextColor =>
            IsNameTab ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(IsInitialLoading));
                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                }
            }
        }

        public bool IsSearchContinuing
        {
            get => _isSearchContinuing;
            private set => SetProperty(ref _isSearchContinuing, value);
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            private set => SetProperty(ref _isLoadingMore, value);
        }

        public bool HasMoreItems
        {
            get => _hasMoreItems;
            private set => SetProperty(ref _hasMoreItems, value);
        }

        public bool IsInitialLoading => IsBusy && Results.Count == 0;

        public string ArticlesText
        {
            get => _articlesText;
            set => SetProperty(ref _articlesText, value);
        }

        public string ProductNameText
        {
            get => _productNameText;
            set => SetProperty(ref _productNameText, value);
        }

        public bool SearchWildberries
        {
            get => _searchWildberries;
            set => SetProperty(ref _searchWildberries, value);
        }

        public bool SearchOzon
        {
            get => _searchOzon;
            set => SetProperty(ref _searchOzon, value);
        }

        public ArticleMarketOption? SelectedArticleMarket
        {
            get => _selectedArticleMarket;
            set
            {
                if (!SetProperty(ref _selectedArticleMarket, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(ArticlesHintText));
                RaisePropertyChanged(nameof(ArticlesPlaceholder));
            }
        }

        public string ArticlesHintText =>
            SelectedArticlesMarketType == MarketType.Ozon
                ? "Через пробел, запятую или с новой строки. Можно вставлять ссылки Ozon."
                : "Через пробел, запятую или с новой строки. Можно вставлять ссылки WB.";

        public string ArticlesPlaceholder =>
            SelectedArticlesMarketType == MarketType.Ozon
                ? "Например: 1678901234\nhttps://www.ozon.ru/product/naushniki-1678901234/\nhttps://ozon.ru/t/RhWvoBC"
                : "Например: 993972254\nhttps://www.wildberries.ru/catalog/993972254/detail.aspx";

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ShowSnackbar(value, isError: true);
                    }

                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                }
            }
        }

        public string SearchProgressText
        {
            get => _searchProgressText;
            private set => SetProperty(ref _searchProgressText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value) && !string.IsNullOrWhiteSpace(value))
                {
                    ShowSnackbar(value, isError: false);
                }
            }
        }

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

        public bool HasResults => Results.Count > 0;

        public bool ShowEmptyMessage => !IsBusy && Results.Count == 0 && string.IsNullOrEmpty(ErrorMessage);

        public string EmptyMessage => IsArticlesTab
            ? "Добавьте артикулы или ссылки — новые товары появятся здесь."
            : "Найдите товары по названию или сразу добавьте их в каталог.";

        public string CountText =>
            Results.Count == 0
                ? string.Empty
                : _pipelineProducts.Count > Results.Count
                    ? $"Показано: {Results.Count} из {_pipelineProducts.Count}"
                    : $"Показано: {Results.Count}";

        private MarketType SelectedArticlesMarketType =>
            SelectedArticleMarket?.MarketType ?? MarketType.Wildberries;

        private string SelectedArticlesMarketLabel =>
            SelectedArticlesMarketType == MarketType.Ozon ? "Ozon" : "WB";

        private bool CanLoadMore() => !IsBusy && !IsLoadingMore && HasMoreItems && IsNameTab;

        private void SelectTab(bool articles)
        {
            if (IsArticlesTab == articles)
            {
                return;
            }

            Interlocked.Increment(ref _nameOpGeneration);
            IsSearchContinuing = false;
            IsArticlesTab = articles;
            _errorMessage = string.Empty;
            _statusMessage = string.Empty;
            RaisePropertyChanged(nameof(ErrorMessage));
            RaisePropertyChanged(nameof(StatusMessage));
            ClearResults();
            RaisePropertyChanged(nameof(ShowEmptyMessage));
        }

        private async Task AddByArticlesAsync()
        {
            try
            {
                IsBusy = true;
                _errorMessage = string.Empty;
                _statusMessage = string.Empty;
                RaisePropertyChanged(nameof(ErrorMessage));
                RaisePropertyChanged(nameof(StatusMessage));
                ClearResults();
                AppLog.Action("AddProducts", "AddByArticles", SelectedArticlesMarketLabel);

                var rawIds = ArticlesText.Split([' ', '\n', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                if (rawIds.Length == 0)
                {
                    ErrorMessage = "Укажите артикулы или ссылки на товары.";
                    return;
                }

                var marketType = SelectedArticlesMarketType;
                var result = await _productsService.AddByArticlesAsync(rawIds, marketType);
                ReplaceResults(result.AddedProducts.Select(ProductListItem.FromProduct), paginate: false);

                ArticlesText = string.Empty;

                var status = result.AddedProducts.Count > 0
                    ? $"Добавлено в каталог: {result.AddedProducts.Count} из {result.FoundCount}."
                    : $"Найдено на {SelectedArticlesMarketLabel}: {result.FoundCount}. Новых нет — уже в каталоге.";

                if (!string.IsNullOrWhiteSpace(result.ValidationErrors))
                {
                    ErrorMessage = $"{status} Часть артикулов пропущена: {result.ValidationErrors}";
                }
                else
                {
                    StatusMessage = status;
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "AddByArticles");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(ShowEmptyMessage));
            }
        }

        private async Task SearchByNameAsync()
        {
            var generation = Interlocked.Increment(ref _nameOpGeneration);
            try
            {
                IsBusy = true;
                IsSearchContinuing = false;
                SearchProgressText = "Ищем товары…";
                _errorMessage = string.Empty;
                _statusMessage = string.Empty;
                RaisePropertyChanged(nameof(ErrorMessage));
                RaisePropertyChanged(nameof(StatusMessage));
                ClearResults();
                AppLog.Action("AddProducts", "SearchByName");

                if (string.IsNullOrWhiteSpace(ProductNameText))
                {
                    ErrorMessage = "Введите название товара.";
                    return;
                }

                var markets = GetSelectedSearchMarkets();
                if (markets.Count == 0)
                {
                    ErrorMessage = "Выберите хотя бы один магазин.";
                    return;
                }

                var query = ProductNameText.Trim();
                _ = EnsureSearchHubConnectedAsync();

                var foreground = markets.Where(m => m == MarketType.Wildberries).ToList();
                var background = markets.Where(m => m == MarketType.Ozon).ToList();
                if (foreground.Count == 0)
                {
                    foreground = background;
                    background = [];
                }

                var found = await SearchMarketsAsync(query, foreground, generation).ConfigureAwait(true);
                if (generation != _nameOpGeneration)
                {
                    return;
                }

                if (background.Count > 0 && Results.Count > 0)
                {
                    StatusMessage = found > 0
                        ? $"Найдено на Wildberries: {found}. Ищем на Ozon…"
                        : "Ищем на Ozon…";
                    IsSearchContinuing = true;
                    SearchProgressText = "Ищем на Ozon…";
                    _ = ContinueSearchInBackgroundAsync(query, background, generation);
                    return;
                }

                if (background.Count > 0)
                {
                    SearchProgressText = "Ищем на Ozon…";
                    found += await SearchMarketsAsync(query, background, generation).ConfigureAwait(true);
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }
                }

                FinishNameSearch(generation);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "SearchByName");
                ErrorMessage = ex.Message;
            }
            finally
            {
                if (generation == _nameOpGeneration)
                {
                    IsBusy = false;
                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                    RaisePropertyChanged(nameof(IsSearchContinuing));
                }
            }
        }

        private async Task AddByNameAsync()
        {
            var generation = Interlocked.Increment(ref _nameOpGeneration);
            try
            {
                IsBusy = true;
                IsSearchContinuing = false;
                SearchProgressText = "Ищем товары…";
                _errorMessage = string.Empty;
                _statusMessage = string.Empty;
                RaisePropertyChanged(nameof(ErrorMessage));
                RaisePropertyChanged(nameof(StatusMessage));
                ClearResults();
                AppLog.Action("AddProducts", "AddByName");

                if (string.IsNullOrWhiteSpace(ProductNameText))
                {
                    ErrorMessage = "Введите название товара.";
                    return;
                }

                var markets = GetSelectedSearchMarkets();
                if (markets.Count == 0)
                {
                    ErrorMessage = "Выберите хотя бы один магазин.";
                    return;
                }

                var query = ProductNameText.Trim();
                _ = EnsureSearchHubConnectedAsync();

                var foreground = markets.Where(m => m == MarketType.Wildberries).ToList();
                var background = markets.Where(m => m == MarketType.Ozon).ToList();
                if (foreground.Count == 0)
                {
                    foreground = background;
                    background = [];
                }

                var (added, found) = await AddMarketsAsync(query, foreground, generation).ConfigureAwait(true);
                if (generation != _nameOpGeneration)
                {
                    return;
                }

                if (background.Count > 0 && Results.Count > 0)
                {
                    StatusMessage = added > 0
                        ? $"Добавлено с Wildberries: {added} из {found}. Ищем на Ozon…"
                        : $"Wildberries: найдено {found}, новых нет. Ищем на Ozon…";
                    IsSearchContinuing = true;
                    SearchProgressText = "Ищем на Ozon…";
                    _ = ContinueAddInBackgroundAsync(query, background, generation, added, found);
                    return;
                }

                if (background.Count > 0)
                {
                    SearchProgressText = "Ищем на Ozon…";
                    var extra = await AddMarketsAsync(query, background, generation).ConfigureAwait(true);
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }

                    added += extra.Added;
                    found += extra.Found;
                }

                FinishNameAdd(added, found, generation);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "AddByName");
                ErrorMessage = ex.Message;
            }
            finally
            {
                if (generation == _nameOpGeneration)
                {
                    IsBusy = false;
                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                    RaisePropertyChanged(nameof(IsSearchContinuing));
                }
            }
        }

        private async Task LoadMoreAsync()
        {
            if (!CanLoadMore())
            {
                return;
            }

            try
            {
                IsLoadingMore = true;
                AppLog.Action("AddProducts", "LoadMore");
                await Task.Yield();
                AppendNextPage();
            }
            finally
            {
                IsLoadingMore = false;
                RaisePropertyChanged(nameof(CountText));
            }
        }

        private List<MarketType> GetSelectedSearchMarkets()
        {
            var markets = new List<MarketType>();
            if (SearchWildberries)
            {
                markets.Add(MarketType.Wildberries);
            }

            if (SearchOzon)
            {
                markets.Add(MarketType.Ozon);
            }

            return markets;
        }

        private void ClearResults()
        {
            _pipelineProducts.Clear();
            Results.Clear();
            HasMoreItems = false;
            RaisePropertyChanged(nameof(CountText));
        }

        private void ReplaceResults(IEnumerable<ProductListItem> items, bool paginate)
        {
            var list = items.ToList();
            var showAdult = _adultContentPreference.ShowAdultContent;
            foreach (var item in list)
            {
                item.ApplyShowAdultContent(showAdult);
            }

            _pipelineProducts.Clear();
            _pipelineProducts.AddRange(list);
            Results.Clear();

            if (!paginate || list.Count <= PageSize)
            {
                foreach (var item in list)
                {
                    Results.Add(item);
                }

                HasMoreItems = false;
                PrefetchProductImages(list);
                RaisePropertyChanged(nameof(CountText));
                return;
            }

            AppendNextPage();
            RaisePropertyChanged(nameof(CountText));
        }

        private void AppendNextPage()
        {
            if (_pipelineProducts.Count == 0)
            {
                HasMoreItems = false;
                return;
            }

            var next = _pipelineProducts
                .Skip(Results.Count)
                .Take(PageSize)
                .ToList();

            if (next.Count == 0)
            {
                HasMoreItems = false;
                return;
            }

            foreach (var item in next)
            {
                Results.Add(item);
            }

            HasMoreItems = Results.Count < _pipelineProducts.Count;
            PrefetchProductImages(next);
        }

        private void ApplyAdultContentPreferenceToAll()
        {
            var show = _adultContentPreference.ShowAdultContent;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in Results)
                {
                    item.ApplyShowAdultContent(show);
                }
            });
        }

        private void PrefetchProductImages(IReadOnlyList<ProductListItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            _ = PrefetchProductImagesAsync(items);
        }

        private async Task PrefetchProductImagesAsync(IReadOnlyList<ProductListItem> items)
        {
            _imagesCts?.Cancel();
            _imagesCts?.Dispose();
            var cts = new CancellationTokenSource();
            _imagesCts = cts;
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

        private async Task<int> SearchMarketsAsync(
            string query,
            IReadOnlyList<MarketType> markets,
            int generation)
        {
            if (markets.Count == 0)
            {
                return 0;
            }

            var found = 0;
            var tasks = markets.Select(async market =>
            {
                try
                {
                    var products = await _productsService
                        .SearchOnWildBerriesAsync(query, [market])
                        .ConfigureAwait(false);
                    if (generation != _nameOpGeneration)
                    {
                        return 0;
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (generation != _nameOpGeneration)
                        {
                            return;
                        }

                        MergeResults(products.Select(ProductListItem.FromProduct));
                    }).ConfigureAwait(false);
                    return products.Count;
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex, "AddProducts", $"SearchByName:{market}");
                    return 0;
                }
            });

            var counts = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var count in counts)
            {
                found += count;
            }

            return found;
        }

        private async Task<(int Added, int Found)> AddMarketsAsync(
            string query,
            IReadOnlyList<MarketType> markets,
            int generation)
        {
            if (markets.Count == 0)
            {
                return (0, 0);
            }

            var added = 0;
            var found = 0;
            var tasks = markets.Select(async market =>
            {
                try
                {
                    var result = await _productsService
                        .AddByNameAsync(query, [market])
                        .ConfigureAwait(false);
                    if (generation != _nameOpGeneration)
                    {
                        return (0, 0);
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (generation != _nameOpGeneration)
                        {
                            return;
                        }

                        MergeResults(result.AddedProducts.Select(ProductListItem.FromProduct));
                    }).ConfigureAwait(false);
                    return (result.AddedProducts.Count, result.FoundCount);
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex, "AddProducts", $"AddByName:{market}");
                    return (0, 0);
                }
            });

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (marketAdded, marketFound) in results)
            {
                added += marketAdded;
                found += marketFound;
            }

            return (added, found);
        }

        private async Task ContinueSearchInBackgroundAsync(
            string query,
            IReadOnlyList<MarketType> markets,
            int generation)
        {
            try
            {
                await SearchMarketsAsync(query, markets, generation).ConfigureAwait(false);
                if (generation != _nameOpGeneration)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }

                    IsSearchContinuing = false;
                    FinishNameSearch(generation);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "SearchByName:Ozon");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }

                    IsSearchContinuing = false;
                    if (Results.Count == 0)
                    {
                        ErrorMessage = ex.Message;
                    }
                }).ConfigureAwait(false);
            }
        }

        private async Task ContinueAddInBackgroundAsync(
            string query,
            IReadOnlyList<MarketType> markets,
            int generation,
            int alreadyAdded,
            int alreadyFound)
        {
            try
            {
                var extra = await AddMarketsAsync(query, markets, generation).ConfigureAwait(false);
                if (generation != _nameOpGeneration)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }

                    IsSearchContinuing = false;
                    FinishNameAdd(alreadyAdded + extra.Added, alreadyFound + extra.Found, generation);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "AddByName:Ozon");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _nameOpGeneration)
                    {
                        return;
                    }

                    IsSearchContinuing = false;
                    if (Results.Count == 0)
                    {
                        ErrorMessage = ex.Message;
                    }
                    else
                    {
                        FinishNameAdd(alreadyAdded, alreadyFound, generation);
                    }
                }).ConfigureAwait(false);
            }
        }

        private void FinishNameSearch(int generation)
        {
            if (generation != _nameOpGeneration)
            {
                return;
            }

            if (Results.Count > 0)
            {
                StatusMessage = $"Найдено: {_pipelineProducts.Count}. Нажмите «Добавить в каталог», чтобы сохранить новые.";
                return;
            }

            ErrorMessage = "По запросу ничего не найдено.";
        }

        private void FinishNameAdd(int added, int found, int generation)
        {
            if (generation != _nameOpGeneration)
            {
                return;
            }

            if (added > 0)
            {
                StatusMessage = $"Добавлено в каталог: {added} из {found}.";
                return;
            }

            if (found > 0)
            {
                StatusMessage = $"Найдено: {found}. Новых нет — уже в каталоге.";
                return;
            }

            if (Results.Count == 0)
            {
                ErrorMessage = "По запросу ничего не найдено на выбранных магазинах.";
            }
        }

        private void MergeResults(IEnumerable<ProductListItem> items)
        {
            var list = items.ToList();
            if (list.Count == 0)
            {
                return;
            }

            var showAdult = _adultContentPreference.ShowAdultContent;
            var added = new List<ProductListItem>();
            foreach (var item in list)
            {
                item.ApplyShowAdultContent(showAdult);
                if (_pipelineProducts.Any(existing =>
                        existing.IdInMarket == item.IdInMarket &&
                        existing.MarketType == item.MarketType))
                {
                    continue;
                }

                _pipelineProducts.Add(item);
                added.Add(item);
            }

            if (added.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                _errorMessage = string.Empty;
                RaisePropertyChanged(nameof(ErrorMessage));
            }

            if (Results.Count < PageSize)
            {
                AppendNextPage();
            }
            else
            {
                HasMoreItems = Results.Count < _pipelineProducts.Count;
            }

            RaisePropertyChanged(nameof(CountText));
            RaisePropertyChanged(nameof(HasResults));
            RaisePropertyChanged(nameof(ShowEmptyMessage));
            RaisePropertyChanged(nameof(IsInitialLoading));
        }

        private async Task EnsureSearchHubConnectedAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(SearchHubConnectTimeout);
                await _searchHubClient.ConnectAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Warning("AddProducts", "SearchHub", ex.Message);
            }
        }

        private void OnSearchProgress(SearchProgress progress)
        {
            if (progress is null || string.IsNullOrWhiteSpace(progress.Message))
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => SearchProgressText = progress.Message);
        }

        public void Destroy()
        {
            Interlocked.Increment(ref _nameOpGeneration);
            IsSearchContinuing = false;
            _searchHubClient.ProgressReceived -= OnSearchProgress;
        }

        private async Task OpenLinkAsync(ProductListItem? item)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Link))
            {
                return;
            }

            if (AdultContentAccess.IsRestricted(item.IsAdult, _adultContentPreference.ShowAdultContent))
            {
                AppLog.Warning("AddProducts", "OpenLink", "restricted");
                await AdultContentAccess.ShowRestrictedAsync();
                return;
            }

            AppLog.Action("AddProducts", "OpenLink", $"id={item.Id}");
            try
            {
                await Launcher.Default.OpenAsync(item.Link);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "OpenLink");
                // ignore launcher errors
            }
        }
    }
}
