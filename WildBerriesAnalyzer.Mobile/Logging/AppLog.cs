using Serilog;

namespace WildBerriesAnalyzer.Mobile.Logging
{
    /// <summary>
    /// Единая точка логирования Mobile (Serilog).
    /// </summary>
    public static class AppLog
    {
        public static ILogger Http { get; } = Log.ForContext("Area", "Http");

        public static ILogger Auth { get; } = Log.ForContext("Area", "Auth");

        public static ILogger Nav { get; } = Log.ForContext("Area", "Nav");

        public static ILogger Ui { get; } = Log.ForContext("Area", "UI");

        public static ILogger Service { get; } = Log.ForContext("Area", "Service");

        public static ILogger App { get; } = Log.ForContext("Area", "App");

        public static void Action(string screen, string action, string? details = null)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                Ui.Information("[{Screen}] {Action}", screen, action);
            }
            else
            {
                Ui.Information("[{Screen}] {Action} | {Details}", screen, action, details);
            }
        }

        public static void Error(Exception ex, string screen, string action, string? details = null)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                Ui.Error(ex, "[{Screen}] {Action} failed", screen, action);
            }
            else
            {
                Ui.Error(ex, "[{Screen}] {Action} failed | {Details}", screen, action, details);
            }
        }

        public static void Warning(string screen, string action, string? details = null)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                Ui.Warning("[{Screen}] {Action}", screen, action);
            }
            else
            {
                Ui.Warning("[{Screen}] {Action} | {Details}", screen, action, details);
            }
        }
    }
}
