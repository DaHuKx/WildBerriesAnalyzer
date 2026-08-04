using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Modules.Auth.ViewModels;
using WildBerriesAnalyzer.Modules.Auth.Views;

namespace WildBerriesAnalyzer.Modules.Auth
{
    public class AuthModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<LoginPage, LoginPageViewModel>(NavigationNames.LoginPage);
        }
    }
}
