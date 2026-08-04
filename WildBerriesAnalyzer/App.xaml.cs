using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using WildBerriesAnalyzer.Business;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Interfaces;
using WildBerriesAnalyzer.Modules.ActualDisconts;
using WildBerriesAnalyzer.Modules.AddById;
using WildBerriesAnalyzer.Modules.AddByName;
using WildBerriesAnalyzer.Modules.Disconts;
using WildBerriesAnalyzer.Modules.Search;
using WildBerriesAnalyzer.Modules.StartPage;
using WildBerriesAnalyzer.PrismCore.Interfaces;
using WildBerriesAnalyzer.PrismCore.Services;
using WildBerriesAnalyzer.Views;

namespace WildBerriesAnalyzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<IWildBerriesService, WildBerriesService>();
            containerRegistry.Register<IDiscontsService, DiscontsService>();
            containerRegistry.RegisterSingleton<ProductIdValidator>();
            containerRegistry.RegisterSingleton<ProductNameValidator>();
            containerRegistry.Register<IProductsService, ProductsService>();
            containerRegistry.Register<INotifier, Notifier>();

            #region Repositories

            containerRegistry.Register<IProductsRepository, ProductsRepository>();
            containerRegistry.Register<IPricesRepository, PricesRepository>();

            #endregion

            containerRegistry.RegisterSingleton<ISynchronizationService, SynchronizationService>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<StartPageModule>();
            moduleCatalog.AddModule<SearchModule>();
            moduleCatalog.AddModule<DiscontsModule>();
            moduleCatalog.AddModule<AddByNameModule>();
            moduleCatalog.AddModule<ActualDiscontsModule>();
            moduleCatalog.AddModule<AddByIdModule>();
        }
    }
}
