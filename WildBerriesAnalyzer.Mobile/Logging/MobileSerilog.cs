using Serilog;
using Serilog.Events;
using WildBerriesAnalyzer.Mobile.Core;

namespace WildBerriesAnalyzer.Mobile.Logging
{
    public static class MobileSerilog
    {
        private static int _initialized;

        public static string? LogDirectory { get; private set; }

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            try
            {
                LogDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
                Directory.CreateDirectory(LogDirectory);

                var logPath = Path.Combine(LogDirectory, "mobile-.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.WithProperty("App", "WildBerriesAnalyzer.Mobile")
                    .Enrich.WithProperty("Version", AppClientVersion.Version)
                    .Enrich.WithProperty("Platform", AppClientVersion.Platform)
                    .WriteTo.Debug(
                        outputTemplate: "[{Level:u3}] {Area} {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        shared: true,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Area} {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                AppLog.App.Information(
                    "Serilog initialized. LogDir={LogDir}, Version={Version}",
                    LogDirectory,
                    AppClientVersion.Version);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Serilog init failed: {ex}");
            }
        }

        public static void AttachGlobalHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    AppLog.App.Fatal(ex, "UnhandledException (IsTerminating={IsTerminating})", e.IsTerminating);
                }
                else
                {
                    AppLog.App.Fatal("UnhandledException: {Object}", e.ExceptionObject);
                }

                Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                AppLog.App.Error(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };

#if ANDROID
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            {
                AppLog.App.Fatal(args.Exception, "Android UnhandledExceptionRaiser");
                Log.CloseAndFlush();
            };
#endif
        }

        public static void Close()
        {
            try
            {
                AppLog.App.Information("Serilog shutting down");
                Log.CloseAndFlush();
            }
            catch
            {
                // ignore
            }
        }
    }
}
