using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.ServerClient.Clients;
using WildBerriesAnalyzer.ServerClient.Handlers;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWildBerriesServerClient(
            this IServiceCollection services,
            Action<WbServerClientOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services.AddWildBerriesServerClient();
        }

        public static IServiceCollection AddWildBerriesServerClient(
            this IServiceCollection services,
            string baseAddress)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

            return services.AddWildBerriesServerClient(options => options.BaseAddress = baseAddress);
        }

        public static IServiceCollection AddWildBerriesServerClient(this IServiceCollection services)
        {
            services.AddOptions<WbServerClientOptions>();
            services.AddSingleton<IWbAuthTokenStore, WbAuthTokenStore>();
            services.AddSingleton<IAuthTokenRefresher>(CreateAuthTokenRefresher);
            services.AddTransient<BearerTokenHandler>();

            services.AddHttpClient<IAuthClient, AuthClient>(ConfigureHttpClient);

            services.AddHttpClient<IFiltersClient, FiltersClient>(ConfigureHttpClient)
                .AddHttpMessageHandler<BearerTokenHandler>();

            services.AddHttpClient<IProductsClient, ProductsClient>(ConfigureHttpClient)
                .AddHttpMessageHandler<BearerTokenHandler>();

            services.AddHttpClient<IDiscontsClient, DiscontsClient>(ConfigureHttpClient)
                .AddHttpMessageHandler<BearerTokenHandler>();

            services.AddHttpClient<IAccountClient, AccountClient>(ConfigureHttpClient)
                .AddHttpMessageHandler<BearerTokenHandler>();

            services.AddSingleton<ISearchHubClient, SearchHubClient>();

            services.AddTransient<IAuthService>(provider => provider.GetRequiredService<IAuthClient>());
            services.AddTransient<IFiltersService>(provider => provider.GetRequiredService<IFiltersClient>());
            services.AddTransient<IProductsService>(provider => provider.GetRequiredService<IProductsClient>());

            return services;
        }

        private static AuthTokenRefresher CreateAuthTokenRefresher(IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<WbServerClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BaseAddress))
            {
                throw new InvalidOperationException(
                    $"Не задан {nameof(WbServerClientOptions.BaseAddress)} для ServerClient.");
            }

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(options.BaseAddress)),
                Timeout = TimeSpan.FromSeconds(60)
            };

            return new AuthTokenRefresher(httpClient, provider.GetRequiredService<IWbAuthTokenStore>());
        }

        private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
        {
            var options = provider.GetRequiredService<IOptions<WbServerClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BaseAddress))
            {
                throw new InvalidOperationException(
                    $"Не задан {nameof(WbServerClientOptions.BaseAddress)} для ServerClient.");
            }

            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseAddress));
        }

        private static string EnsureTrailingSlash(string baseAddress)
        {
            return baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/";
        }
    }
}
