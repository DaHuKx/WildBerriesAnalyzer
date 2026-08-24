using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Server.Hubs;

namespace WildBerriesAnalyzer.Server.Services;

public sealed class SearchHubProgressNotifier : ISearchProgressNotifier
{
    private readonly IHubContext<SearchHub> _hub;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SearchHubProgressNotifier(
        IHubContext<SearchHub> hub,
        IHttpContextAccessor httpContextAccessor)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task NotifyAsync(SearchProgress progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await _hub.Clients.User(userId)
            .SendAsync(SearchHubEvents.Progress, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
