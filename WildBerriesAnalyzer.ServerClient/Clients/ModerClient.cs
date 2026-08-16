using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public interface IModerClient
    {
        Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default);

        Task<ModerProductDto?> GetNextProductAsync(CancellationToken cancellationToken = default);

        Task<List<ModerProductDto>> GetUncategorizedProductsAsync(CancellationToken cancellationToken = default);

        Task<List<ModerCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        Task AssignAsync(ModerAssignRequest request, CancellationToken cancellationToken = default);

        Task<ModerBulkAssignResultDto> AssignBulkAsync(ModerBulkAssignRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class ModerClient : IModerClient
    {
        private readonly HttpClient _httpClient;

        public ModerClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/moder/queue/count", cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            var dto = (await response.Content.ReadFromJsonAsync<ModerQueueCountDto>(WbServerJson.Options, cancellationToken)
                .ConfigureAwait(false))!;
            return dto.Count;
        }

        public async Task<ModerProductDto?> GetNextProductAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/moder/queue/next", cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }

            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ModerProductDto>(WbServerJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<List<ModerProductDto>> GetUncategorizedProductsAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/moder/queue/uncategorized", cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<List<ModerProductDto>>(WbServerJson.Options, cancellationToken)
                .ConfigureAwait(false))!;
        }

        public async Task<List<ModerCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/moder/categories", cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<List<ModerCategoryDto>>(WbServerJson.Options, cancellationToken)
                .ConfigureAwait(false))!;
        }

        public async Task AssignAsync(ModerAssignRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var response = await _httpClient.PostAsJsonAsync(
                    "api/moder/assign",
                    request,
                    WbServerJson.Options,
                    cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
        }

        public async Task<ModerBulkAssignResultDto> AssignBulkAsync(
            ModerBulkAssignRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var response = await _httpClient.PostAsJsonAsync(
                    "api/moder/assign/bulk",
                    request,
                    WbServerJson.Options,
                    cancellationToken)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<ModerBulkAssignResultDto>(WbServerJson.Options, cancellationToken)
                .ConfigureAwait(false))!;
        }
    }
}
