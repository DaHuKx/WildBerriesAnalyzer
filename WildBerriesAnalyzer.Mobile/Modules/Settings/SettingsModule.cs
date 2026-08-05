using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.Settings.ViewModels;
using WildBerriesAnalyzer.Modules.Settings.Views;

namespace WildBerriesAnalyzer.Modules.Settings
{
    public class SettingsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<SettingsPage>();
            containerRegistry.Register<SettingsPageViewModel>();
        }
    }
}
