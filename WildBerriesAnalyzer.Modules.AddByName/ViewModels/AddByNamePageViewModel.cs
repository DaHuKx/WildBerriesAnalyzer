using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.PrismCore.Interfaces;
using WildBerriesAnalyzer.PrismCore.Models;

namespace WildBerriesAnalyzer.Modules.AddByName.ViewModels
{
    public class AddByNamePageViewModel : BindableBase
    {
        #region Приватные поля

        private readonly ISynchronizationService _synchronizationService;
        private readonly IWildBerriesService _wildBerriesService;
        private readonly IProductsRepository _productsRepository;
        private readonly IPricesRepository _priceRepository;

        private string _searchText;
        private ObservableCollection<WbProduct> _products;
        private ICollectionView _productsView;
        private bool _isLoaded;
        private string _status;
        private bool _snackBarShowed;
        private bool _canSaveToDataBase;
        private string _snackBarMessage;

        #endregion

        #region Публичные свойства
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value != _searchText)
                {
                    _searchText = value;
                    RaisePropertyChanged(nameof(SearchText));
                }
            }
        }
        public ObservableCollection<WbProduct> Products
        {
            get => _products;
            set
            {
                if (value != _products)
                {
                    _products = value;
                    RaisePropertyChanged(nameof(Products));
                }
            }
        }
        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                if (value != _isLoaded)
                {
                    _isLoaded = value;
                    RaisePropertyChanged(nameof(IsLoaded));
                }
            }
        }
        public bool SnackBarShowed
        {
            get => _snackBarShowed;
            set
            {
                if (value != _snackBarShowed)
                {
                    _snackBarShowed = value;
                    RaisePropertyChanged(nameof(SnackBarShowed));
                }
            }
        }
        public bool CanSaveToDataBase
        {
            get => _canSaveToDataBase;
            set
            {
                if (value != _canSaveToDataBase)
                {
                    _canSaveToDataBase = value;
                    RaisePropertyChanged(nameof(CanSaveToDataBase));
                }
            }
        }

        public string SnackBarMessage
        {
            get => _snackBarMessage;
            set
            {
                if (value != _snackBarMessage)
                {
                    _snackBarMessage = value;
                    RaisePropertyChanged(nameof(SnackBarMessage));
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (value != _status)
                {
                    _status = value;
                    RaisePropertyChanged(nameof(Status));
                }
            }
        }

        public ICollectionView ProductsView
        {
            get => _productsView;
            set
            {
                if (value != _productsView)
                {
                    _productsView = value;
                    RaisePropertyChanged(nameof(ProductsView));
                }
            }
        }

        public List<Rating> Ratings { get; set; }
        public List<FeedBack> FeedBacks { get; set; }


        public bool ProductsNotEmpty => _products != null && _products.Count > 0;

        #endregion

        #region Публичные команды

        public ICommand SearchProductsCommand => new DelegateCommand(SearchProducts);
        public ICommand AddProductsInDataBaseCommand => new DelegateCommand(AddProductsInDataBase);

        #endregion

        public AddByNamePageViewModel(IWildBerriesService wildBerriesService,
                                      ISynchronizationService synchronizationService,
                                      IProductsRepository productsRepository,
                                      IPricesRepository priceRepository)
        {
            _wildBerriesService = wildBerriesService;
            _productsRepository = productsRepository;
            _priceRepository = priceRepository;
            _synchronizationService = synchronizationService;

            SnackBarMessage = string.Empty;
            SnackBarShowed = false;
            CanSaveToDataBase = false;
            Status = "Введите название товара";

            Ratings = Rating.GetRatings();
            FeedBacks = FeedBack.GetFeedBacksRatio();
        }

        private async void SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                ShowSnackBarWithMessage("Введите название товара.");
                return;
            }

            IsLoaded = false;
            Status = "Получение товаров с WildBerries...";
            CanSaveToDataBase = false;

            try
            {
                Products = new ObservableCollection<WbProduct>(await _wildBerriesService.ParseProductsAsync(_searchText));

                if (Products.Count > 0)
                {
                    CanSaveToDataBase = true;
                }
            }
            catch (Exception ex)
            {
                ShowSnackBarWithMessage($"Не удалось получить продукты. Слишком частые запросы.");
                return;
            }

            IsLoaded = true;

            ShowSnackBarWithMessage($"Найдено {Products.Count} продуктов.");
        }

        private async void AddProductsInDataBase()
        {
            if (Products == null)
            {
                return;
            }

            Status = "Сохранение товара...";
            IsLoaded = false;
            CanSaveToDataBase = false;
            int count = 1;

            var productsForAdd = _products.Where(p => p.FeedBacksCount > 10 &&
                                                      p.ReviewRating > 4);

            int productsCount = (await _productsRepository.AddRangeAsync(productsForAdd)).Count();

            if (productsCount == 0)
            {
                ShowSnackBarWithMessage($"Продукты не добавлены в базу данных. Возможно, они были добавлены ранее.");
            }
            else
            {
                Status = $"Добавление цен товаров ({count}/{productsCount})...";
                await _priceRepository.AddPricesFromProductsAsync(_products);

                ShowSnackBarWithMessage($"{productsCount} продуктов успешно добавлено!");
            }

            _synchronizationService.Synchronize();

            IsLoaded = true;
        }
        private async void ShowSnackBarWithMessage(string message)
        {
            SnackBarMessage = message;

            await Task.Run(() =>
            {
                SnackBarShowed = true;
                Thread.Sleep(4000);
                SnackBarShowed = false;
            });
        }
    }
}
