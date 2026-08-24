using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.ServerClient.Interfaces;
using WildBerriesAnalyzer.ServerClient.Models;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public class ProductsClient : IProductsClient
    {
        private readonly HttpClient _httpClient;

        public ProductsClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<WbProduct?> GetByIdAsync(int id)
        {
            using var response = await _httpClient.GetAsync($"api/products/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return await response.Content.ReadFromJsonAsync<WbProduct>(WbServerJson.Options);
        }

        public async Task<List<WbProduct>> GetByNameAsync(string name)
        {
            var encoded = Uri.EscapeDataString(name ?? string.Empty);
            using var response = await _httpClient.GetAsync($"api/products/name?name={encoded}");
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<WbProduct>>(WbServerJson.Options)) ?? [];
        }

        public async Task<List<WbProduct>> GetRandomAsync(int count)
        {
            using var response = await _httpClient.GetAsync($"api/products/random?count={count}");
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<WbProduct>>(WbServerJson.Options)) ?? [];
        }

        public async Task<long> GetCountAsync()
        {
            using var response = await _httpClient.GetAsync("api/products/count");
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return await response.Content.ReadFromJsonAsync<long>(WbServerJson.Options);
        }

        public async Task<WbPrice> GetLastPriceAsync(int productId)
        {
            using var response = await _httpClient.GetAsync($"api/products/{productId}/last-price");
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<WbPrice>(WbServerJson.Options))!;
        }

        public async Task<ProductPriceHistory> GetPriceHistoryAsync(int productId, PriceHistoryPeriod period)
        {
            using var response = await _httpClient.GetAsync(
                $"api/products/{productId}/prices?period={Uri.EscapeDataString(period.ToString())}");
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<ProductPriceHistory>(WbServerJson.Options))!;
        }

        public List<WbProduct> FilterProductsByName(IEnumerable<WbProduct> products, string productName)
        {
            if (products is null)
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                return products is ICollection<WbProduct> col
                    ? [.. col]
                    : products.ToList();
            }

            return products
                .Where(p => p.Name is not null &&
                            p.Name.Contains(productName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<List<WbProduct>> SearchOnWildBerriesAsync(
            string name,
            IReadOnlyCollection<MarketType>? marketTypes = null)
        {
            var encoded = Uri.EscapeDataString(name ?? string.Empty);
            var url = $"api/products/wb-search?name={encoded}";
            if (marketTypes is { Count: > 0 })
            {
                foreach (var market in marketTypes.Distinct())
                {
                    url += $"&markets={Uri.EscapeDataString(market.ToString())}";
                }
            }

            using var response = await _httpClient.GetAsync(url);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<List<WbProduct>>(WbServerJson.Options)) ?? [];
        }

        public async Task<AddCatalogProductsResult> AddByArticlesAsync(
            IEnumerable<string> articleInputs,
            MarketType marketType = MarketType.Wildberries)
        {
            ArgumentNullException.ThrowIfNull(articleInputs);

            var request = new AddProductsByArticlesRequest
            {
                Articles = articleInputs.Where(a => !string.IsNullOrWhiteSpace(a)).ToList(),
                MarketType = marketType
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/products/by-articles",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<AddCatalogProductsResult>(WbServerJson.Options))!;
        }

        public async Task<AddCatalogProductsResult> AddByNameAsync(
            string name,
            IReadOnlyCollection<MarketType>? marketTypes = null)
        {
            var request = new AddProductsByNameRequest
            {
                Name = name ?? string.Empty,
                MarketTypes = marketTypes is { Count: > 0 }
                    ? marketTypes.Distinct().ToList()
                    : null
            };
            using var response = await _httpClient.PostAsJsonAsync(
                "api/products/by-name",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<AddCatalogProductsResult>(WbServerJson.Options))!;
        }
    }
}
