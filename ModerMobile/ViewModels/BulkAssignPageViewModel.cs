using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModerMobile.Auth;
using ModerMobile.Models;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Clients;

namespace ModerMobile.ViewModels;

public sealed class BulkAssignPageViewModel : INotifyPropertyChanged
{
    private readonly IModerClient _moderClient;
    private readonly IAuthSessionService _session;
    private readonly List<CategoryOption> _allCategories = [];
    private readonly List<ProductOption> _allProducts = [];

    private string _categorySearchText = string.Empty;
    private string _fromIdText = string.Empty;
    private string _toIdText = string.Empty;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public BulkAssignPageViewModel(IModerClient moderClient, IAuthSessionService session)
    {
        _moderClient = moderClient;
        _session = session;
        FilteredCategories = new ObservableCollection<CategoryOption>();
        Products = new ObservableCollection<ProductOption>();

        AddCategoryCommand = new Command(AddCategory, () => CanAddCategory);
        ApplyRangeCommand = new Command(ApplyIdRange, () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        RefreshCommand = new Command(async () => await LoadAsync());
    }

    public ObservableCollection<CategoryOption> FilteredCategories { get; }
    public ObservableCollection<ProductOption> Products { get; }

    public ICommand AddCategoryCommand { get; }
    public ICommand ApplyRangeCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RefreshCommand { get; }

    public string CategorySearchText
    {
        get => _categorySearchText;
        set
        {
            if (Set(ref _categorySearchText, value))
            {
                ApplyCategoryFilter();
                RaiseCanExecute();
            }
        }
    }

    public string FromIdText
    {
        get => _fromIdText;
        set
        {
            if (Set(ref _fromIdText, value))
            {
                ApplyIdRange();
            }
        }
    }

    public string ToIdText
    {
        get => _toIdText;
        set
        {
            if (Set(ref _toIdText, value))
            {
                ApplyIdRange();
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

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanAddCategory =>
        !IsBusy && !string.IsNullOrWhiteSpace(CategorySearchText);

    public bool CanSave =>
        !IsBusy
        && _allCategories.Any(c => c.IsSelected)
        && _allProducts.Any(p => p.IsSelected);

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
            await ReloadProductsAsync();
            ApplyIdRange();
            StatusMessage = $"Без категории: {_allProducts.Count}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.GetBaseException().Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCanExecute();
        }
    }

    private void AddCategory()
    {
        var name = (CategorySearchText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || IsBusy)
        {
            return;
        }

        var existing = _allCategories.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.IsSelected = true;
            CategorySearchText = string.Empty;
            ApplyCategoryFilter();
            RaiseCanExecute();
            return;
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
        CategorySearchText = string.Empty;
        ApplyCategoryFilter();
        RaiseCanExecute();
    }

    private void ApplyIdRange()
    {
        if (!int.TryParse(FromIdText?.Trim(), out var fromId) ||
            !int.TryParse(ToIdText?.Trim(), out var toId))
        {
            return;
        }

        if (fromId > toId)
        {
            (fromId, toId) = (toId, fromId);
        }

        foreach (var product in _allProducts)
        {
            product.IsSelected = product.Id >= fromId && product.Id <= toId;
        }

        var selected = _allProducts.Count(p => p.IsSelected);
        StatusMessage = selected > 0
            ? $"Выделено: {selected} (ID {fromId}–{toId})"
            : $"В диапазоне {fromId}–{toId} нет товаров без категории";
        RaiseCanExecute();
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var selectedProducts = _allProducts.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        var selectedCategories = _allCategories.Where(c => c.IsSelected).ToList();
        if (selectedProducts.Count == 0 || selectedCategories.Count == 0)
        {
            ErrorMessage = "Выберите категории и товары.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _moderClient.AssignBulkAsync(new ModerBulkAssignRequest
            {
                ProductIds = selectedProducts,
                CategoryIds = selectedCategories.Where(c => c.Id > 0 && !c.IsNew).Select(c => c.Id).ToList(),
                NewCategoryNames = selectedCategories
                    .Where(c => c.Id <= 0 || c.IsNew)
                    .Select(c => c.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });

            StatusMessage = $"Сохранено: {result.AssignedCount} товар(ов)";
            FromIdText = string.Empty;
            ToIdText = string.Empty;
            await ReloadCategoriesAsync();
            await ReloadProductsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.GetBaseException().Message;
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
            var option = new CategoryOption { Id = category.Id, Name = category.Name };
            option.PropertyChanged += OnCategoryPropertyChanged;
            _allCategories.Add(option);
        }

        ApplyCategoryFilter();
    }

    private async Task ReloadProductsAsync()
    {
        foreach (var old in _allProducts)
        {
            old.PropertyChanged -= OnProductPropertyChanged;
        }

        _allProducts.Clear();
        Products.Clear();

        var products = await _moderClient.GetUncategorizedProductsAsync();
        foreach (var product in products)
        {
            var option = new ProductOption
            {
                Id = product.Id,
                Name = string.IsNullOrWhiteSpace(product.Name) ? $"Товар #{product.Id}" : product.Name
            };
            option.PropertyChanged += OnProductPropertyChanged;
            _allProducts.Add(option);
            Products.Add(option);
        }
    }

    private void ApplyCategoryFilter()
    {
        var query = (CategorySearchText ?? string.Empty).Trim();
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
            ApplyCategoryFilter();
        }
    }

    private void OnProductPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductOption.IsSelected))
        {
            RaiseCanExecute();
        }
    }

    private void RaiseCanExecute()
    {
        OnPropertyChanged(nameof(CanAddCategory));
        OnPropertyChanged(nameof(CanSave));
        (AddCategoryCommand as Command)?.ChangeCanExecute();
        (ApplyRangeCommand as Command)?.ChangeCanExecute();
        (SaveCommand as Command)?.ChangeCanExecute();
    }

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
