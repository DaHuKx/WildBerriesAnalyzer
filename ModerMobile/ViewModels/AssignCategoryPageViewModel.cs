using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModerMobile.Auth;
using ModerMobile.Models;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Clients;

namespace ModerMobile.ViewModels;

public sealed class AssignCategoryPageViewModel : INotifyPropertyChanged
{
    private readonly IModerClient _moderClient;
    private readonly IAuthSessionService _session;
    private readonly List<CategoryOption> _allCategories = [];
    private int? _currentProductId;
    private string _productName = string.Empty;
    private string _productMeta = string.Empty;
    private string _searchText = string.Empty;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _hasProduct;
    private bool _queueEmpty;

    public AssignCategoryPageViewModel(IModerClient moderClient, IAuthSessionService session)
    {
        _moderClient = moderClient;
        _session = session;
        FilteredCategories = new ObservableCollection<CategoryOption>();
        AddCategoryCommand = new Command(async () => await AddCategoryAsync(), () => CanAddCategory);
        ConfirmCommand = new Command(async () => await ConfirmAsync(), () => CanConfirm);
        RefreshCommand = new Command(async () => await LoadAsync());
    }

    public ObservableCollection<CategoryOption> FilteredCategories { get; }

    public ICommand AddCategoryCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand RefreshCommand { get; }

    public string ProductName
    {
        get => _productName;
        private set => Set(ref _productName, value);
    }

    public string ProductMeta
    {
        get => _productMeta;
        private set => Set(ref _productMeta, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ApplyFilter();
                RaiseCanExecute();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (Set(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                RaiseCanExecute();
            }
        }
    }

    public bool HasProduct
    {
        get => _hasProduct;
        private set => Set(ref _hasProduct, value);
    }

    public bool QueueEmpty
    {
        get => _queueEmpty;
        private set => Set(ref _queueEmpty, value);
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanAddCategory =>
        !IsBusy && HasProduct && !string.IsNullOrWhiteSpace(SearchText);

    public bool CanConfirm =>
        !IsBusy && HasProduct && _allCategories.Any(c => c.IsSelected);

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//LoginPage");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await ReloadCategoriesAsync();
            await LoadNextProductAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = DescribeError(ex);
            HasProduct = false;
            QueueEmpty = false;
            ProductName = string.Empty;
            ProductMeta = string.Empty;
        }
        finally
        {
            IsBusy = false;
            RaiseCanExecute();
        }
    }

    private async Task LoadNextProductAsync()
    {
        var remaining = await _moderClient.GetQueueCountAsync();
        StatusMessage = remaining > 0
            ? $"Без категории: {remaining}"
            : "Очередь пуста";

        var product = await _moderClient.GetNextProductAsync();
        if (product is null)
        {
            _currentProductId = null;
            HasProduct = false;
            QueueEmpty = true;
            ProductName = "Нет товаров без категории";
            ProductMeta = string.Empty;
            ClearSelections();
            return;
        }

        QueueEmpty = false;
        HasProduct = true;
        _currentProductId = product.Id;
        ProductName = string.IsNullOrWhiteSpace(product.Name) ? $"Товар #{product.Id}" : product.Name;
        ProductMeta = $"{product.MarketType} · {product.IdInMarket}"
                      + (string.IsNullOrWhiteSpace(product.Brand) ? string.Empty : $" · {product.Brand}");
        ClearSelections();
        SearchText = string.Empty;
    }

    private Task AddCategoryAsync()
    {
        var name = (SearchText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || IsBusy)
        {
            return Task.CompletedTask;
        }

        var existing = _allCategories.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.IsSelected = true;
            SearchText = string.Empty;
            ApplyFilter();
            RaiseCanExecute();
            return Task.CompletedTask;
        }

        var option = new CategoryOption
        {
            Id = 0,
            Name = name,
            IsSelected = true,
            IsNew = true
        };
        option.PropertyChanged += OnCategoryPropertyChanged;
        _allCategories.Add(option);
        SearchText = string.Empty;
        ApplyFilter();
        RaiseCanExecute();
        return Task.CompletedTask;
    }

    private async Task ConfirmAsync()
    {
        if (_currentProductId is not int productId || IsBusy)
        {
            return;
        }

        var selected = _allCategories.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ErrorMessage = "Выберите хотя бы одну категорию.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var existingIds = selected.Where(c => c.Id > 0 && !c.IsNew).Select(c => c.Id).ToList();
            var newNames = selected.Where(c => c.Id <= 0 || c.IsNew)
                .Select(c => c.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _moderClient.AssignAsync(new ModerAssignRequest
            {
                ProductId = productId,
                CategoryIds = existingIds,
                NewCategoryNames = newNames
            });

            await ReloadCategoriesAsync();
            await LoadNextProductAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = DescribeError(ex);
        }
        finally
        {
            IsBusy = false;
            RaiseCanExecute();
        }
    }

    private async Task ReloadCategoriesAsync()
    {
        foreach (var old in _allCategories)
        {
            old.PropertyChanged -= OnCategoryPropertyChanged;
        }

        _allCategories.Clear();
        var categories = await _moderClient.GetCategoriesAsync();
        foreach (var category in categories)
        {
            var option = new CategoryOption
            {
                Id = category.Id,
                Name = category.Name
            };
            option.PropertyChanged += OnCategoryPropertyChanged;
            _allCategories.Add(option);
        }

        ApplyFilter();
    }

    private void ClearSelections()
    {
        foreach (var category in _allCategories)
        {
            category.IsSelected = false;
        }
    }

    private void ApplyFilter()
    {
        var query = (_searchText ?? string.Empty).Trim();
        FilteredCategories.Clear();

        IEnumerable<CategoryOption> source = _allCategories
            .OrderByDescending(c => c.IsSelected)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var category in source)
        {
            FilteredCategories.Add(category);
        }
    }

    private void OnCategoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CategoryOption.IsSelected))
        {
            RaiseCanExecute();
            ApplyFilter();
        }
    }

    private void RaiseCanExecute()
    {
        OnPropertyChanged(nameof(CanAddCategory));
        OnPropertyChanged(nameof(CanConfirm));
        (AddCategoryCommand as Command)?.ChangeCanExecute();
        (ConfirmCommand as Command)?.ChangeCanExecute();
    }

    private static string DescribeError(Exception ex) =>
        ex.GetBaseException().Message;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
