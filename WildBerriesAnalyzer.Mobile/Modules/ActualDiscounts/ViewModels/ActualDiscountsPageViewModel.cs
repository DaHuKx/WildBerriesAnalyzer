using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.ActualDiscounts.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.ActualDiscounts.ViewModels
{
    public class ActualDiscountsPageViewModel : BindableBase
    {
        private const int PageLimit = 300;
        private const int PageSize = 20;

        private readonly IDiscontsClient _discontsClient;
        private readonly IProductImageCache _productImageCache;
        private readonly INavigationService _navigationService;
        private readonly List<DiscontListItem> _sourceItems = [];
        private List<DiscontListItem> _pipelineItems = [];

        private bool _isBusy;
        private bool _isLoadingMore;
        private bool _hasMoreItems;
        private int _visibleCount;
        private bool _isFilteredTab = true;
        private bool _suppressFilterEvents;
        private string _searchText = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private bool _isFiltersExpanded;
        private DiscontSortOption? _selectedSort;
        private DiscontPercentOption? _selectedPercent;
        private DiscontRatingOption? _selectedRating;
        private DiscontFeedBackOption? _selectedFeedBack;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _imagesCts;

        public ActualDiscountsPageViewModel(
            IDiscontsClient discontsClient,
            IProductImageCache productImageCache,
            INavigationService navigationService)
        {
            _discontsClient = discontsClient;
            _productImageCache = productImageCache;
            _navigationService = navigationService;

            SortOptions = DiscontSortOption.CreateAll();
            PercentOptions = DiscontPercentOption.CreateAll();
            RatingOptions = DiscontRatingOption.CreateAll();
            FeedBackOptions = DiscontFeedBackOption.CreateAll();

            _selectedSort = SortOptions[0];
            _selectedPercent = PercentOptions[0];
            _selectedRating = RatingOptions[0];
            _selectedFeedBack = FeedBackOptions[0];

            ShowFilteredCommand = new DelegateCommand(() => SelectTab(filtered: true), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            ShowAllCommand = new DelegateCommand(() => SelectTab(filtered: false), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            SearchCommand = new DelegateCommand(() => ApplyPipeline(showStatusSnackbar: false), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            ClearFiltersCommand = new DelegateCommand(ClearFilters, () => HasActiveFilters && !IsBusy)
                .ObservesProperty(() => HasActiveFilters)
                .ObservesProperty(() => IsBusy);

            ToggleFiltersCommand = new DelegateCommand(() => IsFiltersExpanded = !IsFiltersExpanded);

            LoadMoreCommand = new DelegateCommand(async () => await LoadMoreAsync(), CanLoadMore)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsLoadingMore)
                .ObservesProperty(() => HasMoreItems);

            OpenProductCommand = new DelegateCommand<DiscontListItem>(async item => await OpenProductAsync(item));
            DismissSnackbarCommand = new DelegateCommand(DismissSnackbar);

            Items.CollectionChanged += (_, _) =>
            {
                RaisePropertyChanged(nameof(ShowEmptyMessage));
                RaisePropertyChanged(nameof(CountText));
                RaisePropertyChanged(nameof(VisibleCountText));
                RaisePropertyChanged(nameof(IsInitialLoading));
            };

            _ = LoadAsync();
        }

        public string Title => "Актуальные скидки";

        public string BrandLabel => "PRICELAB";

        public string Subtitle => "Скидки по вашим фильтрам и по всему каталогу";

        public ObservableCollection<DiscontListItem> Items { get; } = [];

        public List<DiscontSortOption> SortOptions { get; }

        public List<DiscontPercentOption> PercentOptions { get; }

        public List<DiscontRatingOption> RatingOptions { get; }

        public List<DiscontFeedBackOption> FeedBackOptions { get; }

        public DelegateCommand ShowFilteredCommand { get; }

        public DelegateCommand ShowAllCommand { get; }

        public DelegateCommand SearchCommand { get; }

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand ClearFiltersCommand { get; }

        public DelegateCommand ToggleFiltersCommand { get; }

        public DelegateCommand LoadMoreCommand { get; }

        public DelegateCommand DismissSnackbarCommand { get; }

        public DelegateCommand<DiscontListItem> OpenProductCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    RaisePropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public DiscontSortOption? SelectedSort
        {
            get => _selectedSort;
            set => SetFilterOption(ref _selectedSort, value);
        }

        public DiscontPercentOption? SelectedPercent
        {
            get => _selectedPercent;
            set => SetFilterOption(ref _selectedPercent, value);
        }

        public DiscontRatingOption? SelectedRating
        {
            get => _selectedRating;
            set => SetFilterOption(ref _selectedRating, value);
        }

        public DiscontFeedBackOption? SelectedFeedBack
        {
            get => _selectedFeedBack;
            set => SetFilterOption(ref _selectedFeedBack, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(ShowEmptyMessage));
                    RaisePropertyChanged(nameof(IsInitialLoading));
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

        public bool IsInitialLoading => IsBusy && Items.Count == 0 && _sourceItems.Count == 0;

        public bool IsFilteredTab
        {
            get => _isFilteredTab;
            private set
            {
                if (!SetProperty(ref _isFilteredTab, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(IsAllTab));
                RaisePropertyChanged(nameof(FilteredTabBackground));
                RaisePropertyChanged(nameof(AllTabBackground));
                RaisePropertyChanged(nameof(FilteredTabTextColor));
                RaisePropertyChanged(nameof(AllTabTextColor));
                RaisePropertyChanged(nameof(EmptyMessage));
            }
        }

        public bool IsAllTab => !IsFilteredTab;

        public Color FilteredTabBackground =>
            IsFilteredTab ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color AllTabBackground =>
            IsAllTab ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color FilteredTabTextColor =>
            IsFilteredTab ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public Color AllTabTextColor =>
            IsAllTab ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value) && !string.IsNullOrWhiteSpace(value))
                {
                    ShowSnackbar(value, isError: true);
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

        public string CountText =>
            Items.Count == 0
                ? string.Empty
                : Items.Count == _pipelineItems.Count
                    ? $"Найдено: {Items.Count}"
                    : $"Показано: {Items.Count} из {_pipelineItems.Count}";

        public string VisibleCountText =>
            _pipelineItems.Count == 0
                ? "0"
                : Items.Count == _pipelineItems.Count
                    ? Items.Count.ToString()
                    : $"{Items.Count} из {_pipelineItems.Count}";

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchText) ||
            (SelectedSort?.Id ?? 0) != 0 ||
            (SelectedPercent?.Id ?? 0) != 0 ||
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

        public bool ShowEmptyMessage => !IsBusy && Items.Count == 0 && string.IsNullOrEmpty(ErrorMessage);

        public string EmptyMessage
        {
            get
            {
                if (_sourceItems.Count == 0)
                {
                    return IsFilteredTab
                        ? "Нет скидок по вашим фильтрам"
                        : "Актуальные скидки пока не рассчитаны";
                }

                return "Ничего не найдено по текущему поиску и фильтрам";
            }
        }

        private bool CanLoadMore() => !IsBusy && !IsLoadingMore && HasMoreItems;

        private void SetFilterOption<T>(ref T? field, T? value) where T : class
        {
            if (value is null || _suppressFilterEvents)
            {
                return;
            }

            if (!SetProperty(ref field, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasActiveFilters));
            ApplyPipeline(showStatusSnackbar: false);
        }

        private void ClearFilters()
        {
            if (!HasActiveFilters)
            {
                return;
            }

            _searchText = string.Empty;
            _selectedSort = SortOptions[0];
            _selectedPercent = PercentOptions[0];
            _selectedRating = RatingOptions[0];
            _selectedFeedBack = FeedBackOptions[0];

            RaisePropertyChanged(nameof(SearchText));
            RaisePropertyChanged(nameof(SelectedSort));
            RaisePropertyChanged(nameof(SelectedPercent));
            RaisePropertyChanged(nameof(SelectedRating));
            RaisePropertyChanged(nameof(SelectedFeedBack));
            RaisePropertyChanged(nameof(HasActiveFilters));

            ApplyPipeline(showStatusSnackbar: false);
        }

        private void SelectTab(bool filtered)
        {
            if (IsFilteredTab == filtered && _sourceItems.Count > 0)
            {
                return;
            }

            IsFilteredTab = filtered;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            _errorMessage = string.Empty;
            _statusMessage = string.Empty;
            RaisePropertyChanged(nameof(ErrorMessage));
            RaisePropertyChanged(nameof(StatusMessage));

            try
            {
                var disconts = IsFilteredTab
                    ? await _discontsClient.GetForCurrentUserAsync(PageLimit)
                    : await _discontsClient.GetAllAsync(PageLimit);

                _sourceItems.Clear();
                _sourceItems.AddRange(disconts.Select(DiscontListItem.FromDiscont));

                ApplyPipeline(showStatusSnackbar: true);
            }
            catch (Exception ex)
            {
                _sourceItems.Clear();
                _pipelineItems = [];
                _visibleCount = 0;
                Items.Clear();
                HasMoreItems = false;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(ShowEmptyMessage));
                RaisePropertyChanged(nameof(CountText));
                RaisePropertyChanged(nameof(VisibleCountText));
                RaisePropertyChanged(nameof(EmptyMessage));
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
                await Task.Yield();
                AppendNextPage();
            }
            finally
            {
                IsLoadingMore = false;
                RaisePropertyChanged(nameof(CountText));
                RaisePropertyChanged(nameof(VisibleCountText));
            }
        }

        private void ApplyPipeline(bool showStatusSnackbar = false)
        {
            _pipelineItems = BuildPipeline().ToList();
            _visibleCount = 0;
            Items.Clear();
            AppendNextPage();

            if (showStatusSnackbar && _sourceItems.Count > 0)
            {
                StatusMessage = IsFilteredTab
                    ? $"По вашим фильтрам: {_sourceItems.Count}"
                    : $"Все скидки: {_sourceItems.Count}";
            }

            RaisePropertyChanged(nameof(ShowEmptyMessage));
            RaisePropertyChanged(nameof(CountText));
            RaisePropertyChanged(nameof(VisibleCountText));
            RaisePropertyChanged(nameof(EmptyMessage));
            RaisePropertyChanged(nameof(HasActiveFilters));
        }

        private IEnumerable<DiscontListItem> BuildPipeline()
        {
            IEnumerable<DiscontListItem> query = _sourceItems;

            var search = SearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    Contains(item.Name, search) ||
                    Contains(item.Brand, search) ||
                    Contains(item.ArticleText, search));
            }

            if (SelectedPercent is { MinPercent: > 0 })
            {
                var minPercent = SelectedPercent.MinPercent;
                query = query.Where(item => item.DiscontPercent >= minPercent);
            }

            if (SelectedRating is { MinRating: > 0 })
            {
                var minRating = SelectedRating.MinRating;
                query = query.Where(item =>
                    SelectedRating.Id == 4
                        ? item.ReviewRating >= 4.99
                        : item.ReviewRating >= minRating);
            }

            if (SelectedFeedBack is { MinCount: > 0 })
            {
                var minReviews = SelectedFeedBack.MinCount;
                query = query.Where(item => item.FeedBacksCount >= minReviews);
            }

            return (SelectedSort?.Id ?? 0) switch
            {
                1 => query.OrderBy(item => item.CurrentPrice),
                2 => query.OrderByDescending(item => item.ReviewRating),
                3 => query.OrderByDescending(item => item.FeedBacksCount),
                4 => query.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => query.OrderByDescending(item => item.DiscontPercent)
            };
        }

        private void AppendNextPage()
        {
            if (_visibleCount >= _pipelineItems.Count)
            {
                HasMoreItems = false;
                return;
            }

            var next = _pipelineItems
                .Skip(_visibleCount)
                .Take(PageSize)
                .ToList();

            _visibleCount += next.Count;
            foreach (var item in next)
            {
                Items.Add(item);
            }

            HasMoreItems = _visibleCount < _pipelineItems.Count;
            PrefetchImages(next);
        }

        private void PrefetchImages(IReadOnlyList<DiscontListItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            _ = PrefetchImagesAsync(items);
        }

        private async Task PrefetchImagesAsync(IReadOnlyList<DiscontListItem> items)
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

        private static bool Contains(string? source, string value) =>
            !string.IsNullOrEmpty(source) &&
            source.Contains(value, StringComparison.CurrentCultureIgnoreCase);

        private async Task OpenProductAsync(DiscontListItem? item)
        {
            if (item is null || item.ProductId <= 0)
            {
                return;
            }

            var parameters = new NavigationParameters
            {
                { "productId", item.ProductId },
                { KnownNavigationParameters.UseModalNavigation, true }
            };

            await _navigationService.NavigateAsync(NavigationNames.ProductDetail, parameters);
        }
    }
}
