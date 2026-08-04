using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.AddProducts.ViewModels;
using WildBerriesAnalyzer.Modules.AddProducts.Views;

namespace WildBerriesAnalyzer.Modules.AddProducts
{
    public class AddProductsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<AddProductsPage>();
            containerRegistry.Register<AddProductsPageViewModel>();
        }
    }
}
