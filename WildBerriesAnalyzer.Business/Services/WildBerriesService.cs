using System.Net;
using System.Net.Sockets;
using System.Web;
using Newtonsoft.Json;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Options;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.WbScraping;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    /// <summary>
    /// Сервис взаимодействия с WildBerries.
    /// Токены берутся из IWbScrapingAuthStore и обновляются вручную.
    /// </summary>
    public class WildBerriesService : IWildBerriesService
    {
        /// <summary>
        /// Бит adult/18+ в WB <c>viewFlags</c> (проверено по card/search: 18+ товары имеют bit 22).
        /// Bit 32 (<c>4294967296</c>) — параметр <c>hide_vflags</c> запроса, не маркер карточки.
        /// </summary>
        public const long AdultViewFlag = 1L << 22;

        private readonly IWbScrapingAuthStore _authStore;

        public WildBerriesService()
            : this(new FileWbScrapingAuthStore(CreateDefaultOptions()))
        {
        }

        public WildBerriesService(IWbScrapingAuthStore authStore)
        {
            _authStore = authStore ?? throw new ArgumentNullException(nameof(authStore));
        }

        public async Task<List<WbProduct>> ParseProductsAsync(string name)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["ab_testing"] = "false",
                    ["appType"] = "1",
                    ["curr"] = "rub",
                    ["dest"] = "-1257786",
                    ["hide_dtype"] = "15",
                    ["inheritFilters"] = "undefined",
                    ["lang"] = "ru",
                    ["locale"] = "ru",
                    ["query"] = name,
                    ["resultset"] = "catalog",
                    ["sort"] = "popular",
                    ["spp"] = "30",
                    ["suppressSpellcheck"] = "false"
                };

                string queryString = string.Join("&", parameters.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

                string url = $"https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search?{queryString}";

                string responseBody = await SendAuthorizedGetAsync(url);
                WildBerriesProductsResponse? desserializedWbResponse =
                    JsonConvert.DeserializeObject<WildBerriesProductsResponse>(responseBody);

                return InitializeProductsFromResponse(desserializedWbResponse);
            }
            catch
            {
                return new List<WbProduct>();
            }
        }

        public async Task<ParseProductsPricesResult> ParseProductsPricesAsync(IEnumerable<WbProduct> products)
        {
            try
            {
                var productsList = products as IList<WbProduct> ?? products.ToList();
                if (productsList.Count == 0)
                {
                    return new ParseProductsPricesResult();
                }

                var byMarketId = productsList.ToDictionary(p => p.IdInMarket);

                var uriBuilder = new UriBuilder("https://www.wildberries.ru/__internal/u-card/cards/v4/detail");

                var query = HttpUtility.ParseQueryString(string.Empty);
                query["appType"] = "1";
                query["curr"] = "rub";
                query["dest"] = "-1257786";
                query["spp"] = "30";
                query["hide_dtype"] = "15";
                query["mtype"] = "257";
                query["lang"] = "ru";
                query["ab_testing"] = "false";
                query["nm"] = string.Join(";", byMarketId.Keys);
                uriBuilder.Query = query.ToString();

                string responseBody = await SendAuthorizedGetAsync(uriBuilder.Uri.ToString());
                WildBerriesProductsResponse? desserializedWbResponse =
                    JsonConvert.DeserializeObject<WildBerriesProductsResponse>(responseBody);

                var scrapedProducts = InitializeProductsFromResponse(desserializedWbResponse);
                if (scrapedProducts.Count == 0)
                {
                    return ParseProductsPricesResult.Failed(
                        "WB вернул пустой ответ по батчу товаров (возможны протухшие token/cookie).",
                        isAuthFailure: true);
                }

                var prices = new List<WbPrice>();
                var refreshed = new List<WbProduct>();

                foreach (var scraped in scrapedProducts)
                {
                    if (!byMarketId.TryGetValue(scraped.IdInMarket, out var existing))
                    {
                        continue;
                    }

                    existing.Rating = scraped.Rating;
                    existing.ReviewRating = scraped.ReviewRating;
                    existing.FeedBacksCount = scraped.FeedBacksCount;
                    existing.IsAdult = scraped.IsAdult;
                    refreshed.Add(existing);

                    if (scraped.PriceFromInit is null)
                    {
                        continue;
                    }

                    scraped.PriceFromInit.ProductId = existing.Id;
                    prices.Add(scraped.PriceFromInit);
                }

                return new ParseProductsPricesResult
                {
                    Success = true,
                    Prices = prices,
                    ProductsWithRefreshedMeta = refreshed
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return ParseProductsPricesResult.Failed(ex.Message, isAuthFailure: true);
            }
            catch (Exception ex) when (IsNetworkFailure(ex))
            {
                return ParseProductsPricesResult.Failed(ex.Message, isNetworkFailure: true);
            }
            catch (Exception ex)
            {
                return ParseProductsPricesResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Карточки WB по артикулам. API cards/v4/detail стабильно отрабатывает батчами
        /// (~до 100 nm); длинный список одним запросом обрезается/теряется.
        /// </summary>
        public async Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids)
        {
            const int batchSize = 50;
            var allIds = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (allIds.Count == 0)
            {
                return [];
            }

            var result = new List<WbProduct>(allIds.Count);
            var seen = new HashSet<long>();

            foreach (var batch in allIds.Chunk(batchSize))
            {
                var batchProducts = await GetProductsForIdsBatchAsync(batch);
                foreach (var product in batchProducts)
                {
                    if (seen.Add(product.IdInMarket))
                    {
                        result.Add(product);
                    }
                }
            }

            return result;
        }

        private async Task<List<WbProduct>> GetProductsForIdsBatchAsync(IReadOnlyList<string> ids)
        {
            var uriBuilder = new UriBuilder("https://www.wildberries.ru/__internal/u-card/cards/v4/detail");

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["appType"] = "1";
            query["curr"] = "rub";
            query["dest"] = "-1257786";
            query["spp"] = "30";
            query["hide_dtype"] = "15";
            query["mtype"] = "257";
            query["lang"] = "ru";
            query["ab_testing"] = "false";
            query["nm"] = string.Join(";", ids);
            uriBuilder.Query = query.ToString();

            string responseBody = await SendAuthorizedGetAsync(uriBuilder.Uri.ToString());
            WildBerriesProductsResponse? desserializedWbResponse =
                JsonConvert.DeserializeObject<WildBerriesProductsResponse>(responseBody);

            return InitializeProductsFromResponse(desserializedWbResponse);
        }

        public async Task<List<string>> GetArticlesFromBasketShareAsync(string shareId)
        {
            if (string.IsNullOrWhiteSpace(shareId))
            {
                throw new ArgumentException("shareId пустой.", nameof(shareId));
            }

            shareId = shareId.Trim();
            // Публичный gateway: без Bearer. data_v2?shareId= только обогащает items из тела запроса.
            var url =
                $"https://wbx-api-gateway.wildberries.ru/share-basket/api/v1/basket/{Uri.EscapeDataString(shareId)}";
            var referer = $"https://www.wildberries.ru/lk/basket?shareId={Uri.EscapeDataString(shareId)}";

            var responseBody = await SendPublicGetAsync(url, referer);
            WbShareBasketResponse? parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<WbShareBasketResponse>(responseBody);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Возникла проблема. Попробуйте позже.",
                    ex);
            }

            var items = parsed?.Items;
            if (items is null || items.Count == 0)
            {
                throw new InvalidOperationException(
                    "Общая корзина пуста или ссылка недействительна.");
            }

            return items
                .Select(i => i.NmId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
        }

        private async Task<string> SendPublicGetAsync(string url, string? referer)
        {
            using var handler = Ipv4Http.CreateHandler();
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation(
                "accept-language",
                "ru,en-US;q=0.9,en;q=0.8");
            request.Headers.TryAddWithoutValidation("origin", "https://www.wildberries.ru");
            request.Headers.TryAddWithoutValidation(
                "referer",
                string.IsNullOrWhiteSpace(referer)
                    ? "https://www.wildberries.ru/"
                    : referer);
            request.Headers.TryAddWithoutValidation(
                "user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-site");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    "Общая корзина не найдена или ссылка устарела.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"WB share-basket HTTP {(int)response.StatusCode}");
            }

            return body;
        }

        private async Task<string> SendAuthorizedGetAsync(string url) =>
            await SendAuthorizedAsync(HttpMethod.Get, url, content: null, referer: null);

        private async Task<string> SendAuthorizedAsync(
            HttpMethod method,
            string url,
            HttpContent? content,
            string? referer)
        {
            var auth = _authStore.GetSnapshot();
            if (string.IsNullOrWhiteSpace(auth.AccessToken))
            {
                throw new UnauthorizedAccessException(
                    "WB AccessToken пустой. Обновите вручную (oauth-bff-token.json / ConsoleTest / WbScrapingAuth).");
            }

            using var handler = Ipv4Http.CreateHandler();
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            using var request = new HttpRequestMessage(method, url)
            {
                Content = content
            };
            ApplyRequestHeaders(request, auth, referer);

            using var response = await client.SendAsync(request);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException(
                    "WB вернул 401/403. AccessToken протух — обновите вручную: " +
                    "скопируйте JSON ответа www.wildberries.ru/oauth-bff/api/v1/token " +
                    "в oauth-bff-token.json и запустите ConsoleTest, либо правьте wb-scraping-auth.json.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static void ApplyRequestHeaders(
            HttpRequestMessage request,
            WbScrapingAuthState auth,
            string? referer = null)
        {
            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation(
                "accept-language",
                "ru,en-US;q=0.9,en;q=0.8,zh-CN;q=0.7,zh;q=0.6,de;q=0.5,es;q=0.4");
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {auth.AccessToken}");
            request.Headers.TryAddWithoutValidation("deviceid", auth.DeviceId);
            request.Headers.TryAddWithoutValidation("priority", "u=1, i");
            request.Headers.TryAddWithoutValidation("origin", "https://www.wildberries.ru");
            request.Headers.TryAddWithoutValidation(
                "referer",
                string.IsNullOrWhiteSpace(referer) ? "https://www.wildberries.ru/" : referer);
            request.Headers.TryAddWithoutValidation("sec-ch-ua", auth.SecChUa);
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
            request.Headers.TryAddWithoutValidation("user-agent", auth.UserAgent);
            request.Headers.TryAddWithoutValidation("x-requested-with", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("x-spa-version", auth.SpaVersion);
            request.Headers.TryAddWithoutValidation("cookie", auth.Cookie);
        }

        private List<WbProduct> InitializeProductsFromResponse(WildBerriesProductsResponse? response)
        {
            List<WbProduct> products = new List<WbProduct>();

            if (response == null)
            {
                return products;
            }

            foreach (var product in response.products)
            {
                string productId = product.id.ToString();

                try
                {
                    products.Add(new WbProduct()
                    {
                        MarketType = MarketType.Wildberries,
                        IdInMarket = product.id,
                        Brand = product.brand,
                        FeedBacksCount = product.feedbacks,
                        IsAdult = IsAdultProduct(product.viewFlags),
                        Link = $"https://www.wildberries.ru/catalog/{productId}/detail.aspx",
                        Name = product.name,
                        Rating = product.rating,
                        ReviewRating = product.reviewRating,
                        ImageUrl = WbProductImageUrlBuilder.BuildBigImageUrl(product.id),
                        PriceFromInit = new WbPrice()
                        {
                            CheckTime = DateTime.UtcNow,
                            Price = ExtractPriceRub(product)
                        }
                    });
                }
                catch
                {
                    Thread.Sleep(1);
                }
            }

            return products;
        }

        public static bool IsAdultProduct(long viewFlags) =>
            (viewFlags & AdultViewFlag) != 0;

        private static bool IsNetworkFailure(Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is HttpRequestException or SocketException or IOException)
                {
                    return true;
                }

                var message = current.Message;
                if (message.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("cannot assign requested address", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("No such host", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Цена в рублях из карточки WB (в API суммы в копейках).
        /// </summary>
        private static decimal ExtractPriceRub(Product product)
        {
            var sizeWithPrice = product.sizes?.FirstOrDefault(s => s.price is not null);
            if (sizeWithPrice?.price is null)
            {
                return 0;
            }

            var kopecks = sizeWithPrice.price.product > 0
                ? sizeWithPrice.price.product
                : sizeWithPrice.price.basic;

            return kopecks > 0 ? kopecks / 100m : 0;
        }

        /// <summary>
        /// Начальные значения, если нет appsettings / wb-scraping-auth.json.
        /// </summary>
        public static WbScrapingAuthOptions CreateDefaultOptions()
        {
            return new WbScrapingAuthOptions
            {
                AccessToken =
                    "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRwczovL2lkLndiLnJ1Iiwic3ViIjoiMTA0Nzg2NDA2IiwiYXVkIjpbImh0dHBzOi8vd3d3LndpbGRiZXJyaWVzLnJ1Il0sImV4cCI6MTc4ODI0NjIzNSwibmJmIjoxNzg1NjU0MjM1LCJpYXQiOjE3ODU2NTQyMzUsInVzZXIiOiIxMDQ3ODY0MDYiLCJzaGFyZF9rZXkiOiIxMSIsImNsaWVudF9pZCI6Im1hcmtldHBsYWNlX3dlYiIsInNlc3Npb25faWQiOiI4ODEzMWNkOWFiYjQ0MjBmYTUxYmZjNDMwM2FjNDE0MSIsInZhbGlkYXRpb25fa2V5IjoiMTNmMGExNmMzOWJkZmI2YmMwNDczNmY4OWQ4ZmEwNzRmM2E0YWU0MTFlNWRmYzlhODcxNTM4YTdmM2ExZjBlNSIsImFwcGxpY2F0aW9uIjoid2JfbWFya2V0cGxhY2UiLCJzY29wZSI6WyJvcGVuaWQiLCJwaG9uZSIsInJlYWQ6cHJvZmlsZSIsInJlYWQ6ZW1haWwiXSwidXNlcl9yZWdpc3RyYXRpb25fZHQiOjE2ODMxMTU2MzIsInR5cGUiOiJhY2Nlc3NfdG9rZW4iLCJ2ZXJzaW9uIjo1fQ.B7hZ85eAesx_PL_qS2vTV7ovts9KAWHpzzjOG5x_fuqpUXMXvzgaCjpSjOB6sPzS_teKCx3cGs4hOQC9iwmJ1Nu4XCmuAbY8Pvl7exJ7B_YE8Jh8G4zSICLvvpsYzwwwjMFcw1NSmhjcyl0oE4OpWZy8JPvqdfVLpmUw2rVOSfMfdMucQFm_iZyuEB-jOZirh8jsahv19-FCjpYKlkKNUkBm9xwrrNkvy2dY3GMpK5iVQ3Oh4Asm0Mk9AU91KI5AhuC5ROyu2ik8Bg6aB9pxL6CFezupxb_8HekT_HToS501h-qyDfvMn_npzz9472TfnYgYkLbRjHwtgeqO7JYJxw",
                Cookie =
                    "_wbauid=6924380261783539770; device_id=9af005c3-f8d3-4b05-9289-06df4528e1f3; _cp=1; wbx-validation-key=f23e1a8b-6170-4cfc-9d2a-cad3f2fd8838; x_wbaas_token=1.1000.32d2b759807e4502b0bbb77da0a27ed5.MHwzNy4xMTMuMjE0Ljg2fE1vemlsbGEvNS4wIChXaW5kb3dzIE5UIDEwLjA7IFdpbjY0OyB4NjQpIEFwcGxlV2ViS2l0LzUzNy4zNiAoS0hUTUwsIGxpa2UgR2Vja28pIENocm9tZS8xNDkuMC4wLjAgU2FmYXJpLzUzNy4zNiBPUFIvMTMzLjAuMC4wIChFZGl0aW9uIFl4IEdYKXwxNzg1OTEyOTEyfHJldXNhYmxlfDJ8ZXlKb1lYTm9Jam9pSW4wPXwwfDN8MTc4NTc4MzMxMnwx.MEQCIDy+tgPvPbOkl1/em1HIi8vk57Y9E73UI/CdDys7mLr0AiBwXVfrzPZaGGisG7JmlKC9jPDFf4UMYT+NUCN1vfvIIg==",
                DeviceId = "site_23f3ade8eecd45d0975f5b011a90edca",
                PersistFilePath = "wb-scraping-auth.json"
            };
        }
    }
}
