using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Services.Interfaces;

public interface ISearchProgressNotifier
{
    Task NotifyAsync(SearchProgress progress, CancellationToken cancellationToken = default);
}
