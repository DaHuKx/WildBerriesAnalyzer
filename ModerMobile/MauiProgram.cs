using System.Net.Http;
using Microsoft.Extensions.Logging;
using ModerMobile.Auth;
using ModerMobile.ViewModels;
using WildBerriesAnalyzer.ServerClient;
using WildBerriesAnalyzer.ServerClient.Clients;
using WildBerriesAnalyzer.ServerClient.Handlers;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace ModerMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var tokenStore = new WbAuthTokenStore();
        builder.Services.AddSingleton<IWbAuthTokenStore>(tokenStore);

        var authHttp = CreateHttpClient(CreateClientVersionHandler());
        var authClient = new AuthClient(authHttp, tokenStore);
        builder.Services.AddSingleton<IAuthClient>(authClient);

        var tokenRefresher = new AuthTokenRefresher(
            CreateHttpClient(CreateClientVersionHandler()),
            tokenStore);
        builder.Services.AddSingleton<IAuthTokenRefresher>(tokenRefresher);

        var moderHttp = CreateHttpClient(
            new BearerTokenHandler(tokenStore, tokenRefresher)
            {
                InnerHandler = CreateClientVersionHandler()
            });
        builder.Services.AddSingleton<IModerClient>(new ModerClient(moderHttp));

        builder.Services.AddSingleton<IAuthSessionService, AuthSessionService>();
        builder.Services.AddSingleton<IVkIdLoginService, VkIdLoginService>();

        builder.Services.AddTransient<LoginPageViewModel>();
        builder.Services.AddTransient<MainMenuPageViewModel>();
        builder.Services.AddTransient<AssignCategoryPageViewModel>();
        builder.Services.AddTransient<BulkAssignPageViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(ServerSettings.BaseAddress),
            Timeout = TimeSpan.FromSeconds(60)
        };
        return client;
    }

    private static HttpMessageHandler CreateClientVersionHandler() =>
        new ClientVersionHandler(static () => "1.0.0", "moder")
        {
            InnerHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            }
        };
}
