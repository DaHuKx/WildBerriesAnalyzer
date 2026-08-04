using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.Products.ViewModels;
using WildBerriesAnalyzer.Modules.Products.Views;

namespace WildBerriesAnalyzer.Modules.Products
{
    public class ProductsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<ProductsPage>();
            containerRegistry.Register<ProductsPageViewModel>();
        }
    }
}
