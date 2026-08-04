using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.ActualDiscounts.ViewModels;
using WildBerriesAnalyzer.Modules.ActualDiscounts.Views;

namespace WildBerriesAnalyzer.Modules.ActualDiscounts
{
    public class ActualDiscountsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<ActualDiscountsPage>();
            containerRegistry.Register<ActualDiscountsPageViewModel>();
        }
    }
}
