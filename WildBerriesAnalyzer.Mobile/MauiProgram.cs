using Microsoft.Extensions.Logging;
using Serilog;
using UraniumUI;
using WildBerriesAnalyzer.Mobile.Logging;

namespace WildBerriesAnalyzer.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MobileSerilog.Initialize();
            MobileSerilog.AttachGlobalHandlers();

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseUraniumUI()
                .UseUraniumUIMaterial()
                .UsePrism(PrismStartup.Configure)
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // После UraniumUI Material, чтобы наш mapper шёл последним.
#if ANDROID
            AndroidEntryHandlers.Configure();
#endif

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(dispose: false);

            var app = builder.Build();
            AppLog.App.Information("MauiApp built");
            return app;
        }
    }
}
