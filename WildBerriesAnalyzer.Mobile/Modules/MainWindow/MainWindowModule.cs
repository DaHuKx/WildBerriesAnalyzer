using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Modules.MainWindow.ViewModels;
using WildBerriesAnalyzer.Modules.MainWindow.Views;

namespace WildBerriesAnalyzer.Modules.MainWindow
{
    public class MainWindowModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainWindowPage, MainWindowPageViewModel>(NavigationNames.MainWindow);
            containerRegistry.Register<HomePage>();
            containerRegistry.Register<HomePageViewModel>();
        }
    }
}
