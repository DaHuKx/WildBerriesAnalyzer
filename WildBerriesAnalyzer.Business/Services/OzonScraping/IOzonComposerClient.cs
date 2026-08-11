using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

public interface IOzonComposerClient : IAsyncDisposable, IDisposable
{
    Task<OzonComposerPage> FetchPageAsync(string sitePath, CancellationToken ct = default);
}
