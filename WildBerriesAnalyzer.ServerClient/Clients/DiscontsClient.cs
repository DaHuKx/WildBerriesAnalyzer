using System.Net.Http.Json;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public sealed class DiscontsClient : IDiscontsClient
    {
        private readonly HttpClient _httpClient;

        public DiscontsClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<List<Discont>> GetForCurrentUserAsync(
            int? limit = 50,
            CancellationToken cancellationToken = default)
        {
            var url = limit is null ? "api/disconts" : $"api/disconts?limit={limit}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<Discont>>(WbServerJson.Options, cancellationToken))
                   ?? [];
        }

        public async Task<List<Discont>> GetAllAsync(
            int? limit = 100,
            CancellationToken cancellationToken = default)
        {
            var url = limit is null ? "api/disconts/all" : $"api/disconts/all?limit={limit}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<Discont>>(WbServerJson.Options, cancellationToken))
                   ?? [];
        }

        public async Task<List<Discont>> GetForUserAsync(
            int userId,
            int? limit = 50,
            CancellationToken cancellationToken = default)
        {
            var url = limit is null
                ? $"api/disconts/{userId}"
                : $"api/disconts/{userId}?limit={limit}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<Discont>>(WbServerJson.Options, cancellationToken))
                   ?? [];
        }
    }
}
