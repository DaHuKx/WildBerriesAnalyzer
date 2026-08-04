using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public sealed class AccountClient : IAccountClient
    {
        private readonly HttpClient _httpClient;

        public AccountClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<AccountProfile> GetMeAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/account/me", cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<AccountProfile>(WbServerJson.Options, cancellationToken))!;
        }

        public async Task<VkLinkCodeResult> CreateVkLinkCodeAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync("api/account/vk/link-code", content: null, cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<VkLinkCodeResult>(WbServerJson.Options, cancellationToken))!;
        }

    }
}
