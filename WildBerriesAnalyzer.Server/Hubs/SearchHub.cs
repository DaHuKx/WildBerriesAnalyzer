using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WildBerriesAnalyzer.Server.Hubs;

[Authorize]
public class SearchHub : Hub
{
}
