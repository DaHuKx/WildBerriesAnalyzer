using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public sealed class DashboardClient : IDashboardClient
    {
        private readonly HttpClient _httpClient;

        public DashboardClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<HomeDashboardSummary> GetHomeAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient
                .GetAsync("api/dashboard/home", cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content
                       .ReadFromJsonAsync<HomeDashboardSummary>(WbServerJson.Options, cancellationToken)
                       .ConfigureAwait(false))
                   ?? new HomeDashboardSummary();
        }
    }
}
