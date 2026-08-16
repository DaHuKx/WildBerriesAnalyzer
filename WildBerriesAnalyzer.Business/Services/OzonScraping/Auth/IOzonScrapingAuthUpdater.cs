namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;

/// <summary>
/// Ручное обновление cookie Ozon (админ-команды бота).
/// </summary>
public interface IOzonScrapingAuthUpdater
{
    string PersistFilePath { get; }

    string? LastError { get; }

    bool ApplyCookie(string cookie);

    /// <summary>
    /// Перечитывает cookie с диска в live-options.
    /// </summary>
    bool TryReloadCookieFromDisk();
}
