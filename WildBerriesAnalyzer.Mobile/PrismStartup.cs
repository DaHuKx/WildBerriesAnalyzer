using System.Net.Http;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Mobile.Clients;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Settings;
using WildBerriesAnalyzer.Modules.ActualDiscounts;
using WildBerriesAnalyzer.Modules.AddProducts;
using WildBerriesAnalyzer.Modules.Auth;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.Modules.MainWindow;
using WildBerriesAnalyzer.Modules.MyFilters;
using WildBerriesAnalyzer.Modules.ProductDetail;
using WildBerriesAnalyzer.Modules.Products;
using WildBerriesAnalyzer.ServerClient;
using WildBerriesAnalyzer.ServerClient.Clients;
using WildBerriesAnalyzer.ServerClient.Handlers;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Mobile
{
    public static class PrismStartup
    {
        public static void Configure(PrismAppBuilder builder)
        {
            builder
                .RegisterTypes(RegisterTypes)
                .ConfigureModuleCatalog(ConfigureModuleCatalog)
                .CreateWindow(CreateWindow);
        }

        private static void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var tokenStore = new WbAuthTokenStore();
            containerRegistry.RegisterInstance<IWbAuthTokenStore>(tokenStore);

            var authHttpClient = CreateHttpClient(CreateClientVersionHandler());
            var authClient = new AuthClient(authHttpClient, tokenStore);
            containerRegistry.RegisterInstance<IAuthClient>(authClient);
            containerRegistry.RegisterInstance<IAuthService>(authClient);

            var tokenRefresher = new AuthTokenRefresher(CreateHttpClient(CreateClientVersionHandler()), tokenStore);
            containerRegistry.RegisterInstance<IAuthTokenRefresher>(tokenRefresher);

            var filtersHttpClient = CreateHttpClient(CreateAuthenticatedHandler(tokenStore, tokenRefresher));
            var filtersClient = new FiltersClient(filtersHttpClient);
            containerRegistry.RegisterInstance<IFiltersClient>(filtersClient);
            containerRegistry.RegisterInstance<IFiltersService>(filtersClient);

            var productsHttpClient = CreateHttpClient(CreateAuthenticatedHandler(tokenStore, tokenRefresher));
            var productsClient = new ProductsClient(productsHttpClient);
            containerRegistry.RegisterInstance<IProductsClient>(productsClient);
            containerRegistry.RegisterInstance<IProductsService>(productsClient);

            var discontsHttpClient = CreateHttpClient(CreateAuthenticatedHandler(tokenStore, tokenRefresher));
            var discontsClient = new DiscontsClient(discontsHttpClient);
            containerRegistry.RegisterInstance<IDiscontsClient>(discontsClient);

            var dashboardHttpClient = CreateHttpClient(CreateAuthenticatedHandler(tokenStore, tokenRefresher));
            var dashboardClient = new DashboardClient(dashboardHttpClient);
            containerRegistry.RegisterInstance<IDashboardClient>(dashboardClient);

            containerRegistry.RegisterSingleton<IAuthSessionService, AuthSessionService>();
            containerRegistry.RegisterSingleton<IAuthSessionGuard, AuthSessionGuard>();
            containerRegistry.RegisterSingleton<IVkIdLoginService, VkIdLoginService>();
            containerRegistry.RegisterSingleton<IAppThemeService, AppThemeService>();
            containerRegistry.RegisterSingleton<IProductImageCache, ProductImageCache>();
            containerRegistry.RegisterInstance<IPendingShareStore>(PendingShareStore.Instance);
            containerRegistry.RegisterSingleton<IWbShareToBagService, WbShareToBagService>();
        }

        private static void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<AuthModule>();
            moduleCatalog.AddModule<MainWindowModule>();
            moduleCatalog.AddModule<ProductsModule>();
            moduleCatalog.AddModule<ProductDetailModule>();
            moduleCatalog.AddModule<AddProductsModule>();
            moduleCatalog.AddModule<ActualDiscountsModule>();
            moduleCatalog.AddModule<MyFiltersModule>();
            moduleCatalog.AddModule<SettingsModule>();
        }

        /// <summary>
        /// Только быстрая навигация на Login. Любой network/refresh — после показа UI
        /// (иначе Prism держит splash, пока CreateWindow не завершится).
        /// </summary>
        private static Task CreateWindow(IContainerProvider container, INavigationService navigationService)
        {
            _ = container.Resolve<IAppThemeService>();
            container.Resolve<IAuthSessionGuard>().Attach(navigationService);
            return navigationService.NavigateAsync($"/{NavigationNames.LoginPage}");
        }

        private static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
        {
            var client = handler is null
                ? new HttpClient(CreateHttpMessageHandler(), disposeHandler: true)
                : new HttpClient(handler, disposeHandler: true);

            client.BaseAddress = new Uri(ServerSettings.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        private static HttpMessageHandler CreateAuthenticatedHandler(
            IWbAuthTokenStore tokenStore,
            IAuthTokenRefresher tokenRefresher) =>
            new BearerTokenHandler(tokenStore, tokenRefresher)
            {
                InnerHandler = CreateClientVersionHandler()
            };

        private static HttpMessageHandler CreateClientVersionHandler() =>
            new ClientVersionHandler(
                static () => AppClientVersion.Version,
                AppClientVersion.Platform)
            {
                InnerHandler = CreateHttpMessageHandler()
            };

        private static HttpMessageHandler CreateHttpMessageHandler() =>
            new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
    }
}
