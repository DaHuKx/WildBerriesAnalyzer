namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    public interface IWbScrapingAuthStore
    {
        WbScrapingAuthState GetSnapshot();

        void Update(Action<WbScrapingAuthState> update);

        string PersistFilePath { get; }
    }
}
