namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    public interface IWbScrapingAuthStore
    {
        WbScrapingAuthState GetSnapshot();

        void Update(Action<WbScrapingAuthState> update);

        string PersistFilePath { get; }

        /// <summary>
        /// AccessToken или Cookie изменились (локальный Update или reload с диска).
        /// </summary>
        event EventHandler? CredentialsChanged;
    }
}
