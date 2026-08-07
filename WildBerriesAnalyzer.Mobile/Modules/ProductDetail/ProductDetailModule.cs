using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Modules.ProductDetail.ViewModels;
using WildBerriesAnalyzer.Modules.ProductDetail.Views;

namespace WildBerriesAnalyzer.Modules.ProductDetail
{
    public class ProductDetailModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ProductDetailPage, ProductDetailPageViewModel>(
                NavigationNames.ProductDetail);
        }
    }
}
