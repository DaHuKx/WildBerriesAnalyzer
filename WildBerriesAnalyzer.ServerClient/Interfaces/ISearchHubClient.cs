using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.ServerClient.Interfaces;

/// <summary>
/// Клиент SignalR-хаба поиска (<c>/hubs/search</c>).
/// </summary>
public interface ISearchHubClient : IAsyncDisposable
{
    bool IsConnected { get; }

    event Action<SearchProgress>? ProgressReceived;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

