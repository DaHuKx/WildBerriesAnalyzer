using System.Collections.ObjectModel;
using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.Modules.Products.Models;

namespace WildBerriesAnalyzer.Modules.Products.ViewModels
{
    public class ProductsPageViewModel : BindableBase
    {
        private const int PageSize = 15;

        private readonly IProductsService _productsService;
        private readonly IFiltersService _filtersService;
        private readonly IAuthSessionService _authSessionService;
        private readonly IProductImageCache _productImageCache;
        private readonly INavigationService _navigationService;

        private readonly List<ProductListItem> _sourceProducts = [];
        private readonly HashSet<int> _bagProductIds = [];
        private List<ProductListItem> _pipelineProducts = [];
        private int _visibleCount;
        private long? _catalogTotalCount;
        private bool _suppressFilterEvents;
        private bool _isBrowseMode = true;
        private bool _isBusy;
        private bool _isLoadingMore;
        private bool _hasMoreItems = true;
        private string _searchText = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private bool _isFiltersExpanded;
        private ProductSortOption? _selectedSort;
        private ProductRatingOption? _selectedRating;
        private ProductFeedBackOption? _selectedFeedBack;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _imagesCts;

        public ProductsPageViewModel(
            IProductsService productsService,
            IFiltersService filtersService,
            IAuthSessionService authSessionService,
            IProductImageCache productImageCache,
            INavigationService navigationService)
        {
            _productsService = productsService;
            _filtersService = filtersService;
            _authSessionService = authSessionService;
            _productImageCache = productImageCache;
            _navigationService = navigationService;

            SortOptions = ProductSortOption.CreateAll();
            RatingOptions = ProductRatingOption.CreateAll();
            FeedBackOptions = ProductFeedBackOption.CreateAll();

            _selectedSort = SortOptions[0];
            _selectedRating = RatingOptions[0];
            _selectedFeedBack = FeedBackOptions[0];

            SearchCommand = new DelegateCommand(async () => await SearchAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            RefreshCommand = new DelegateCommand(async () => await LoadInitialAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            LoadMoreCommand = new DelegateCommand(async () => await LoadMoreAsync(), CanLoadMore)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoadingMore)
                .ObservesProperty(() => HasMoreItems);

            ClearFiltersCommand = new DelegateCommand(ClearFilters, () => HasActiveFilters && !IsBusy)
                .ObservesProperty(() => HasActiveFilters)
                .ObservesProperty(() => IsBusy);

            ToggleFiltersCommand = new DelegateCommand(() => IsFiltersExpanded = !IsFiltersExpanded);

            OpenProductCommand = new DelegateCommand<ProductListItem>(async item => await OpenProductAsync(item));
            ToggleBagCommand = new DelegateCommand<ProductListItem>(async item => await ToggleBagAsync(item), _ => !IsBusy)
                .ObservesProperty(() => IsBusy);
            DismissSnackbarCommand = new DelegateCommand(DismissSnackbar);

            Products.CollectionChanged += (_, _) =>
            {
                RaisePropertyChanged(nameof(ShowEmptyMessage));
                RaisePropertyChanged(nameof(VisibleCountText));
                RaisePropertyChanged(nameof(IsInitialLoading));
            };

            _ = LoadInitialAsync();
        }

        public string Title => "Товары";

        public string BrandLabel => "PRICELAB";

        public string Subtitle => "Поиск, просмотр и добавление товаров в корзину";

        public ObservableCollection<ProductListItem> Products { get; } = [];

        public List<ProductSortOption> SortOptions { get; }

        public List<ProductRatingOption> RatingOptions { get; }

        public List<ProductFeedBackOption> FeedBackOptions { get; }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public ProductSortOption? SelectedSort
        {
            get => _selectedSort;
            set
            {
                if (value is null || _suppressFilterEvents)
                {
                    return;
                }

                if (_selectedSort?.Id == value.Id)
                {
                    _selectedSort = value;
                    return;
                }

                if (!SetProperty(ref _selectedSort, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(HasActiveFilters));
                RebuildVisible(reset: true);
                RaiseVisibleCountChanged();
            }
        }

        public ProductRatingOption? SelectedRating
        {
            get => _selectedRating;
            set
            {
                if (value is null || _suppressFilterEvents)
                {
                    return;
                }

                if (_selectedRating?.Id == value.Id)
                {
                    _selectedRating = value;
                    return;
                }

                if (!SetProperty(ref _selectedRating, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(HasActiveFilters));
                RebuildVisible(reset: true);
                RaiseVisibleCountChanged();
            }
        }

        public ProductFeedBackOption? SelectedFeedBack
        {
            get => _selectedFeedBack;
            set
            {
                if (value is null || _suppressFilterEvents)
                {
                    return;
                }

                if (_selectedFeedBack?.Id == value.Id)
                {
                    _selectedFeedBack = value;
                    return;
                }

                if (!SetProperty(ref _selectedFeedBack, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(HasActiveFilters));
                RebuildVisible(reset: true);
                RaiseVisibleCountChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                    RaisePropertyChanged(nameof(IsInitialLoading));
                }
            }
        }

        public bool IsInitialLoading => IsBusy && Products.Count == 0;

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set => SetProperty(ref _isLoadingMore, value);
        }

        public bool HasMoreItems
        {
            get => _hasMoreItems;
            set => SetProperty(ref _hasMoreItems, value);
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

        public bool ShowEmptyMessage => !IsBusy && Products.Count == 0;

        public bool HasActiveFilters =>
            (SelectedSort?.Id ?? 0) != 0 ||
            (SelectedRating?.Id ?? 0) != 0 ||
            (SelectedFeedBack?.Id ?? 0) != 0;

        public bool IsFiltersExpanded
        {
            get => _isFiltersExpanded;
            set
            {
                if (SetProperty(ref _isFiltersExpanded, value))
                {
                    RaisePropertyChanged(nameof(FiltersExpandIcon));
                }
            }
        }

        public string FiltersExpandIcon => IsFiltersExpanded ? "▲" : "▼";

        public string VisibleCountText
        {
            get
            {
                if (_isBrowseMode)
                {
                    return _catalogTotalCount is long total
                        ? $"{Products.Count} из {total}"
                        : Products.Count.ToString();
                }

                return $"{Products.Count} из {_pipelineProducts.Count}";
            }
        }

        public DelegateCommand SearchCommand { get; }

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand LoadMoreCommand { get; }

        public DelegateCommand ClearFiltersCommand { get; }

        public DelegateCommand ToggleFiltersCommand { get; }

        public DelegateCommand DismissSnackbarCommand { get; }

        public DelegateCommand<ProductListItem> OpenProductCommand { get; }

        public DelegateCommand<ProductListItem> ToggleBagCommand { get; }

        private bool CanLoadMore() => !IsBusy && !IsLoadingMore && HasMoreItems;

        private void ClearFilters()
        {
            if (!HasActiveFilters)
            {
                return;
            }

            _selectedSort = SortOptions[0];
            _selectedRating = RatingOptions[0];
            _selectedFeedBack = FeedBackOptions[0];

            RaisePropertyChanged(nameof(SelectedSort));
            RaisePropertyChanged(nameof(SelectedRating));
            RaisePropertyChanged(nameof(SelectedFeedBack));
            RaisePropertyChanged(nameof(HasActiveFilters));

            RebuildVisible(reset: true);
            RaiseVisibleCountChanged();
        }

        private async Task LoadInitialAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                _statusMessage = string.Empty;
                RaisePropertyChanged(nameof(StatusMessage));
                RaisePropertyChanged(nameof(HasStatus));
                _isBrowseMode = true;

                await RefreshBagIdsAsync();

                var products = await _productsService.GetRandomAsync(PageSize);
                _sourceProducts.Clear();
                AddUniqueToSource(products.Select(ProductListItem.FromProduct));

                RebuildVisible(reset: true);
                HasMoreItems = true;

                _catalogTotalCount = await _productsService.GetCountAsync();
                NotifyStatusAfterLoad();
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

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadInitialAsync();
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                _statusMessage = string.Empty;
                RaisePropertyChanged(nameof(StatusMessage));
                RaisePropertyChanged(nameof(HasStatus));
                _isBrowseMode = false;

                await RefreshBagIdsAsync();

                var products = await _productsService.GetByNameAsync(SearchText.Trim());
                _sourceProducts.Clear();
                AddUniqueToSource(products.Select(ProductListItem.FromProduct));

                RebuildVisible(reset: true);
                HasMoreItems = _visibleCount < _pipelineProducts.Count;
                NotifyStatusAfterLoad();
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

        private async Task LoadMoreAsync()
        {
            if (!CanLoadMore())
            {
                return;
            }

            try
            {
                IsLoadingMore = true;
                ErrorMessage = string.Empty;

                if (_isBrowseMode)
                {
                    await AppendBrowseBatchAsync();
                }
                else
                {
                    AppendNextPage();
                    HasMoreItems = _visibleCount < _pipelineProducts.Count;
                }

                RaiseVisibleCountChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        /// <summary>
        /// Догружает новую порцию и только добавляет элементы в конец — без Clear/пересборки списка,
        /// чтобы ScrollView не сбрасывал скролл.
        /// </summary>
        private async Task AppendBrowseBatchAsync()
        {
            var batch = await _productsService.GetRandomAsync(PageSize);
            var newItems = batch
                .Select(ProductListItem.FromProduct)
                .Where(item => _sourceProducts.All(p => p.Id != item.Id))
                .ToList();

            if (newItems.Count == 0)
            {
                var beforeCount = Products.Count;
                AppendMissingFromPipeline(PageSize);
                if (Products.Count == beforeCount)
                {
                    HasMoreItems = false;
                }

                return;
            }

            AddUniqueToSource(newItems);

            var toAppend = ApplyFilterAndSort(newItems)
                .Where(item => Products.All(p => p.Id != item.Id))
                .ToList();

            foreach (var item in toAppend)
            {
                Products.Add(item);
            }

            _pipelineProducts = ApplyFilterAndSort(_sourceProducts).ToList();
            _visibleCount = Products.Count;
            HasMoreItems = true;
            PrefetchProductImages(toAppend);
        }

        private void RebuildVisible(bool reset)
        {
            _pipelineProducts = ApplyFilterAndSort(_sourceProducts).ToList();

            if (reset)
            {
                _visibleCount = 0;
                Products.Clear();
            }

            AppendNextPage();
            HasMoreItems = _isBrowseMode || _visibleCount < _pipelineProducts.Count;
        }

        private void AppendNextPage()
        {
            AppendMissingFromPipeline(PageSize);
        }

        private void AppendMissingFromPipeline(int take)
        {
            if (_pipelineProducts.Count == 0)
            {
                return;
            }

            var next = _pipelineProducts
                .Where(p => Products.All(x => x.Id != p.Id))
                .Take(take)
                .ToList();

            foreach (var item in next)
            {
                Products.Add(item);
            }

            _visibleCount = Products.Count;
            PrefetchProductImages(next);
        }

        private void NotifyStatusAfterLoad()
        {
            RaiseVisibleCountChanged();
            if (_isBrowseMode)
            {
                StatusMessage = _catalogTotalCount is long total
                    ? $"Показано {Products.Count} из {total} в каталоге"
                    : $"Показано {Products.Count}";
                return;
            }

            StatusMessage = $"Найдено {_pipelineProducts.Count} (показано {Products.Count})";
        }

        private void RaiseVisibleCountChanged()
        {
            RaisePropertyChanged(nameof(VisibleCountText));
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

        private IEnumerable<ProductListItem> ApplyFilterAndSort(IEnumerable<ProductListItem> source)
        {
            var query = source.AsEnumerable();

            var ratingId = SelectedRating?.Id ?? 0;
            if (ratingId != 0)
            {
                query = query.Where(p => p.ReviewRating >= ratingId);
            }

            var feedbackCount = SelectedFeedBack?.Count ?? 0;
            var feedbackId = SelectedFeedBack?.Id ?? 0;
            if (feedbackId != 0)
            {
                query = query.Where(p => p.FeedBacksCount >= feedbackCount);
            }

            return (SelectedSort?.Id ?? 0) switch
            {
                1 => query.OrderBy(p => p.LastPrice),
                2 => query.OrderByDescending(p => p.ReviewRating),
                3 => query.OrderByDescending(p => p.FeedBacksCount),
                4 => query.OrderBy(p => p.MedianPrice <= 0 ? decimal.MaxValue : p.LastPrice / p.MedianPrice),
                _ => query
            };
        }

        private void AddUniqueToSource(IEnumerable<ProductListItem> items)
        {
            foreach (var item in items)
            {
                if (_sourceProducts.All(p => p.Id != item.Id))
                {
                    item.IsInBag = _bagProductIds.Contains(item.Id);
                    _sourceProducts.Add(item);
                }
            }
        }

        private async Task ToggleBagAsync(ProductListItem? item)
        {
            if (item is null || item.Id <= 0)
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

                if (item.IsInBag)
                {
                    await _filtersService.RemoveProductsFromBagAsync(user.Id, [item.Id]);
                    _bagProductIds.Remove(item.Id);
                    item.IsInBag = false;
                    StatusMessage = "Товар удалён из корзины.";
                    return;
                }

                if (item.IdInMarket <= 0)
                {
                    ErrorMessage = "У товара нет артикула.";
                    return;
                }

                var article = item.IdInMarket.ToString(CultureInfo.InvariantCulture);
                var result = await _filtersService.AddProductsToBagAsync(user.Id, [article]);
                _bagProductIds.Add(item.Id);
                item.IsInBag = true;
                StatusMessage = result.AddedProducts.Count > 0
                    ? "Товар добавлен в корзину."
                    : "Товар уже есть в корзине.";
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

        private async Task RefreshBagIdsAsync()
        {
            _bagProductIds.Clear();

            var user = _authSessionService.CurrentUser;
            if (user is null || user.Id <= 0)
            {
                return;
            }

            try
            {
                var bag = await _filtersService.GetBagProductsAsync(user.Id);
                foreach (var product in bag)
                {
                    _bagProductIds.Add(product.Id);
                }
            }
            catch
            {
                // Корзина недоступна — иконки останутся в режиме «добавить».
            }
        }

        private async Task OpenProductAsync(ProductListItem? item)
        {
            if (item is null || item.Id <= 0)
            {
                return;
            }

            // Modal: MainWindow остаётся под деталкой (иначе Prism заменяет страницу,
            // GoBack уходит на Login → снова MainWindow с домашним экраном).
            var parameters = new NavigationParameters
            {
                { "productId", item.Id },
                { KnownNavigationParameters.UseModalNavigation, true }
            };

            await _navigationService.NavigateAsync(NavigationNames.ProductDetail, parameters);
        }
    }
}
