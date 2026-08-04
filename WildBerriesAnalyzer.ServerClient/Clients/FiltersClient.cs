using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.ServerClient.Interfaces;
using WildBerriesAnalyzer.ServerClient.Models;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public class FiltersClient : IFiltersClient
    {
        private readonly HttpClient _httpClient;

        public FiltersClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<UserFilterData> GetUserFilterDataAsync(int userId)
        {
            // ConfigureAwait(false): на Android dispose HttpResponse на UI-потоке
            // даёт NetworkOnMainThreadException.
            using var response = await _httpClient.GetAsync($"api/filters/{userId}").ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<UserFilterData>(WbServerJson.Options).ConfigureAwait(false))!;
        }

        public async Task<WbFilter> GetOrCreateByUserIdAsync(int userId)
        {
            using var response = await _httpClient.GetAsync($"api/filters/{userId}/filter").ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<WbFilter>(WbServerJson.Options).ConfigureAwait(false))!;
        }

        public async Task UpdateFilterAsync(WbFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            using var response = await _httpClient.PutAsJsonAsync(
                $"api/filters/{filter.UserId}/filter",
                filter,
                WbServerJson.Options).ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
        }

        public async Task<List<WbProduct>> GetBagProductsAsync(int userId)
        {
            using var response = await _httpClient.GetAsync($"api/filters/{userId}/bag").ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<List<WbProduct>>(WbServerJson.Options).ConfigureAwait(false)) ?? [];
        }

        public async Task<AddBagProductsResult> AddProductsToBagAsync(int userId, IEnumerable<string> articleInputs)
        {
            ArgumentNullException.ThrowIfNull(articleInputs);

            var request = new AddBagProductsRequest
            {
                Articles = articleInputs.Where(a => !string.IsNullOrWhiteSpace(a)).ToList()
            };

            using var response = await _httpClient.PostAsJsonAsync($"api/filters/{userId}/bag", request, WbServerJson.Options)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<AddBagProductsResult>(WbServerJson.Options).ConfigureAwait(false))!;
        }

        public async Task RemoveProductsFromBagAsync(int userId, IEnumerable<int> productIds)
        {
            ArgumentNullException.ThrowIfNull(productIds);

            var request = new RemoveBagProductsRequest
            {
                ProductIds = productIds.ToList()
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/filters/{userId}/bag")
            {
                Content = JsonContent.Create(request, options: WbServerJson.Options)
            };

            using var response = await _httpClient.SendAsync(httpRequest).ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
        }

        public async Task<List<WbFilterCategory>> GetFilterCategoriesAsync(int userId)
        {
            using var response = await _httpClient.GetAsync($"api/filters/{userId}/categories").ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
            return (await response.Content.ReadFromJsonAsync<List<WbFilterCategory>>(WbServerJson.Options).ConfigureAwait(false)) ?? [];
        }

        public async Task AddFilterCategoryAsync(int userId, int categoryId, CategoryFilterType type)
        {
            var request = new AddFilterCategoryRequest
            {
                CategoryId = categoryId,
                Type = type
            };

            using var response = await _httpClient.PostAsJsonAsync($"api/filters/{userId}/categories", request, WbServerJson.Options)
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
        }

        public async Task RemoveFilterCategoryAsync(int userId, int filterCategoryId)
        {
            using var response = await _httpClient.DeleteAsync($"api/filters/{userId}/categories/{filterCategoryId}")
                .ConfigureAwait(false);
            await WbServerJson.EnsureSuccessOrThrowAsync(response).ConfigureAwait(false);
        }
    }
}
