using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.AddProducts.Models;
using WildBerriesAnalyzer.Modules.Products.Models;

namespace WildBerriesAnalyzer.Modules.AddProducts.ViewModels
{
    public class AddProductsPageViewModel : BindableBase
    {
        private const int PageSize = 15;

        private readonly IProductsService _productsService;
        private readonly IProductImageCache _productImageCache;
        private readonly IAdultContentPreferenceService _adultContentPreference;

        private readonly List<ProductListItem> _pipelineProducts = [];

        private bool _isArticlesTab = true;
        private bool _isBusy;
        private bool _isLoadingMore;
        private bool _hasMoreItems;
        private string _articlesText = string.Empty;
        private string _productNameText = string.Empty;
        private bool _searchWildberries = true;
        private bool _searchOzon = true;
        private ArticleMarketOption? _selectedArticleMarket;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _imagesCts;

        public AddProductsPageViewModel(
            IProductsService productsService,
            IProductImageCache productImageCache,
            IAdultContentPreferenceService adultContentPreference)
        {
            _productsService = productsService;
            _productImageCache = productImageCache;
            _adultContentPreference = adultContentPreference;
            _adultContentPreference.Changed += (_, _) => ApplyAdultContentPreferenceToAll();

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
            try
            {
                IsBusy = true;
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

                var products = await _productsService.SearchOnWildBerriesAsync(ProductNameText.Trim(), markets);
                ReplaceResults(products.Select(ProductListItem.FromProduct), paginate: true);

                StatusMessage = products.Count > 0
                    ? $"Найдено: {products.Count}. Нажмите «Добавить в каталог», чтобы сохранить новые."
                    : "По запросу ничего не найдено.";
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "SearchByName");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(ShowEmptyMessage));
            }
        }

        private async Task AddByNameAsync()
        {
            try
            {
                IsBusy = true;
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

                var result = await _productsService.AddByNameAsync(ProductNameText.Trim(), markets);
                ReplaceResults(result.AddedProducts.Select(ProductListItem.FromProduct), paginate: true);

                StatusMessage = result.AddedProducts.Count > 0
                    ? $"Добавлено в каталог: {result.AddedProducts.Count} из {result.FoundCount}."
                    : $"Найдено: {result.FoundCount}. Новых нет — уже в каталоге.";
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "AddProducts", "AddByName");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(ShowEmptyMessage));
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
