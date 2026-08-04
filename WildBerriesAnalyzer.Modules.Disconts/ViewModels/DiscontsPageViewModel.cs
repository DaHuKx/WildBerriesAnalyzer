using Prism.Commands;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enum;
using WildBerriesAnalyzer.PrismCore.Models;
using WildBerriesAnalyzer.PrismCore.Services;

namespace WildBerriesAnalyzer.Modules.Disconts.ViewModels
{
    public class DiscontsPageViewModel : BindableBase
    {
        private const int ITEMS_COUNT_ON_PAGE = 15;

        private readonly IProductsRepository _productsRepository;
        private readonly IDiscontsService _discontsService;
        private readonly IProductsService _productService;

        private int _selectedPage;
        private ObservableCollection<WbChartProduct> _allProducts;
        private ObservableCollection<WbChartProduct> _products;
        private ObservableCollection<WbChartProduct> _viewProducts;
        private ObservableCollection<int> _pagesNumbers;
        private bool _discontsLoaded;
        private string _loadingStatus;
        private string _filteredName;
        private decimal _minPrice;
        private decimal _maxPrice;


        public int SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (value != _selectedPage)
                {
                    _selectedPage = value;
                    ChangePage(_selectedPage);
                }
            }
        }

        public ObservableCollection<WbChartProduct> ViewProducts
        {
            get => _viewProducts;
            set
            {
                if (value != _viewProducts)
                {
                    _viewProducts = value;
                    RaisePropertyChanged(nameof(ViewProducts));
                }
            }
        }

        public ObservableCollection<int> PagesNumbers
        {
            get => _pagesNumbers;
            set
            {
                if (value != _pagesNumbers)
                {
                    _pagesNumbers = value;
                    RaisePropertyChanged(nameof(PagesNumbers));
                }
            }
        }

        public bool DiscontsLoaded
        {
            get => _discontsLoaded;
            set
            {
                if (value != _discontsLoaded)
                {
                    _discontsLoaded = value;
                    RaisePropertyChanged(nameof(DiscontsLoaded));
                }
            }
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set
            {
                if (value != _loadingStatus)
                {
                    _loadingStatus = value;
                    RaisePropertyChanged(nameof(LoadingStatus));
                }
            }
        }

        public string FilteredName
        {
            get => _filteredName;
            set
            {
                if (value != _filteredName)
                {
                    _filteredName = value;
                    RaisePropertyChanged(nameof(FilteredName));
                }
            }
        }

        public decimal MinPrice
        {
            get => _minPrice;
            set
            {
                if (value != _minPrice)
                {
                    _minPrice = value;
                    RaisePropertyChanged(nameof(MinPrice));
                }
            }
        }

        public decimal MaxPrice
        {
            get => _maxPrice;
            set
            {
                if (value != _maxPrice)
                {
                    _maxPrice = value;
                    RaisePropertyChanged(nameof(MaxPrice));
                }
            }
        }

        public DiscontsPageViewModel(IDiscontsService productsService, IProductsRepository productsRepository, IProductsService productService)
        {
            _productsRepository = productsRepository;
            _discontsService = productsService;
            _productService = productService;
            _selectedPage = 0;

            InitializeDiscontsAsync();
        }

        public ICommand ExecuteFiltersCommand => new DelegateCommand(ExecuteFilters);
        public ICommand ChangePageCommand => new DelegateCommand<int>(ChangePage);

        private void ChangePage(int pageNumber)
        {
            int rightInterval = pageNumber * ITEMS_COUNT_ON_PAGE;
            int leftInterval = rightInterval - ITEMS_COUNT_ON_PAGE;

            List<WbChartProduct> products = new List<WbChartProduct>();

            for (int i = leftInterval; i < rightInterval; i++)
            {
                if (i >= _products.Count)
                {
                    break;
                }

                products.Add(_products[i]);
            }

            _selectedPage = pageNumber;
            ViewProducts = new ObservableCollection<WbChartProduct>(products);
        }

        private async void ExecuteFilters()
        {
            await Task.Run(() =>
            {
                DiscontsLoaded = false;

                IEnumerable<WbChartProduct> products;

                if (!string.IsNullOrWhiteSpace(FilteredName))
                {
                    products = WbChartProduct.ConvertToWbChartProducts(_productService.FilterProductsByName(_allProducts, FilteredName));
                }
                else
                {
                    products = new List<WbChartProduct>(_allProducts);
                }

                if (MinPrice != 0)
                {
                    products = products.Where(product => product.LastPrice.Price >= MinPrice);
                }

                if (MaxPrice != 0)
                {
                    products = products.Where(product => product.LastPrice.Price <= MaxPrice);
                }

                foreach (WbChartProduct product in products)
                {
                    var prod = _allProducts.First(pr => pr.Id == product.Id);

                    product.Chart = prod.Chart;
                    product.ChartLabels = prod.ChartLabels;
                }

                _products = new ObservableCollection<WbChartProduct>(products);

                InitializePages();

                ChangePage(1);

                DiscontsLoaded = true;
            });
        }

        private async void InitializeDiscontsAsync()
        {
            DiscontsLoaded = false;

            LoadingStatus = "Получение списка продуктов...";

            var products = await _productsRepository.GetProductsWithPricesAsync();

            LoadingStatus = "Определение лучших скидок среди продуктов...";

            var initProducts = _discontsService.GetDiscontsFromProducts(products, ReferencePriceStrategy.Median);

            LoadingStatus = "Создание графиков цен продуктов...";

            _allProducts = await ChartService.InitializeWbChartProducts(initProducts);
            _products = _allProducts;
            InitializePages();

            ChangePage(1);

            DiscontsLoaded = true;
        }

        private void InitializePages()
        {
            int pagesCount;
            if (_products.Count % ITEMS_COUNT_ON_PAGE == 0)
            {
                pagesCount = _products.Count / ITEMS_COUNT_ON_PAGE;
            }
            else
            {
                pagesCount = _products.Count / ITEMS_COUNT_ON_PAGE + 1;
            }

            PagesNumbers = new ObservableCollection<int>(Enumerable.Range(1, pagesCount));
        }
    }
}
