using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Helpers;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Products.Models;

namespace WildBerriesAnalyzer.Modules.AddProducts.ViewModels
{
    public class AddProductsPageViewModel : BindableBase
    {
        private readonly IProductsService _productsService;
        private readonly IProductImageCache _productImageCache;

        private bool _isArticlesTab = true;
        private bool _isBusy;
        private string _articlesText = string.Empty;
        private string _productNameText = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _snackbarMessage = string.Empty;
        private bool _isSnackbarVisible;
        private bool _isSnackbarError;
        private CancellationTokenSource? _snackbarCts;
        private CancellationTokenSource? _imagesCts;

        public AddProductsPageViewModel(
            IProductsService productsService,
            IProductImageCache productImageCache)
        {
            _productsService = productsService;
            _productImageCache = productImageCache;

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

            OpenLinkCommand = new DelegateCommand<ProductListItem>(async item => await OpenLinkAsync(item));
            DismissSnackbarCommand = new DelegateCommand(DismissSnackbar);
            CopyBasketBookmarkletCommand = new DelegateCommand(async () => await CopyBasketBookmarkletAsync());

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

        public DelegateCommand ShowArticlesTabCommand { get; }

        public DelegateCommand ShowNameTabCommand { get; }

        public DelegateCommand AddByArticlesCommand { get; }

        public DelegateCommand SearchByNameCommand { get; }

        public DelegateCommand AddByNameCommand { get; }

        public DelegateCommand DismissSnackbarCommand { get; }

        public DelegateCommand CopyBasketBookmarkletCommand { get; }

        public DelegateCommand<ProductListItem> OpenLinkCommand { get; }

        public string BasketImportGuide =>
            "На компьютере: скопируйте закладку → создайте закладку в браузере (вставьте текст в адрес) → откройте корзину WB, прокрутите вниз → нажмите закладку → вставьте артикулы сюда.";

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
                : $"Показано: {Results.Count}";

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
            Results.Clear();
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
                Results.Clear();

                var rawIds = ArticlesText.Split([' ', '\n', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                if (rawIds.Length == 0)
                {
                    ErrorMessage = "Укажите артикулы или ссылки на товары.";
                    return;
                }

                var result = await _productsService.AddByArticlesAsync(rawIds);
                ReplaceResults(result.AddedProducts.Select(ProductListItem.FromProduct));

                ArticlesText = string.Empty;

                var status = result.AddedProducts.Count > 0
                    ? $"Добавлено в каталог: {result.AddedProducts.Count} из {result.FoundCount}."
                    : $"Найдено на WB: {result.FoundCount}. Новых нет — уже в каталоге.";

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
                Results.Clear();

                if (string.IsNullOrWhiteSpace(ProductNameText))
                {
                    ErrorMessage = "Введите название товара.";
                    return;
                }

                var products = await _productsService.SearchOnWildBerriesAsync(ProductNameText.Trim());
                ReplaceResults(products.Select(ProductListItem.FromProduct));

                StatusMessage = products.Count > 0
                    ? $"Найдено на WB: {products.Count}. Нажмите «Добавить в каталог», чтобы сохранить новые."
                    : "По запросу ничего не найдено.";
            }
            catch (Exception ex)
            {
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
                Results.Clear();

                if (string.IsNullOrWhiteSpace(ProductNameText))
                {
                    ErrorMessage = "Введите название товара.";
                    return;
                }

                var result = await _productsService.AddByNameAsync(ProductNameText.Trim());
                ReplaceResults(result.AddedProducts.Select(ProductListItem.FromProduct));

                StatusMessage = result.AddedProducts.Count > 0
                    ? $"Добавлено в каталог: {result.AddedProducts.Count} из {result.FoundCount}."
                    : $"Найдено на WB: {result.FoundCount}. Новых нет — уже в каталоге.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(ShowEmptyMessage));
            }
        }

        private void ReplaceResults(IEnumerable<ProductListItem> items)
        {
            Results.Clear();
            var list = items.ToList();
            foreach (var item in list)
            {
                Results.Add(item);
            }

            PrefetchProductImages(list);
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

        private async Task CopyBasketBookmarkletAsync()
        {
            try
            {
                await Clipboard.Default.SetTextAsync(WbBasketBookmarklet.BookmarkletUri);
                StatusMessage = "Закладка скопирована. Вставьте её в адрес новой закладки браузера на компьютере.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось скопировать закладку: {ex.Message}";
            }
        }

        private static async Task OpenLinkAsync(ProductListItem? item)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Link))
            {
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(item.Link);
            }
            catch
            {
                // ignore launcher errors
            }
        }
    }
}
