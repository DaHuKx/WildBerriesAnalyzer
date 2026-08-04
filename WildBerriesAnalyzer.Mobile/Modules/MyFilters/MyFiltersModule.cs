using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.MyFilters.ViewModels;
using WildBerriesAnalyzer.Modules.MyFilters.Views;

namespace WildBerriesAnalyzer.Modules.MyFilters
{
    public class MyFiltersModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<MyFiltersPage>();
            containerRegistry.Register<MyFiltersPageViewModel>();
        }
    }
}
