using Prism.Ioc;
using Prism.Modularity;
using WildBerriesAnalyzer.Modules.Account.ViewModels;
using WildBerriesAnalyzer.Modules.Account.Views;

namespace WildBerriesAnalyzer.Modules.Account
{
    public class AccountModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<AccountPage>();
            containerRegistry.Register<AccountPageViewModel>();
        }
    }
}
