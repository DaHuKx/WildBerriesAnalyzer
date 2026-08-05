using System.Net;
using System.Net.Sockets;
using System.Web;
using Newtonsoft.Json;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Options;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.WbScraping;
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
                    ["hide_vflags"] = "4294967296",
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
                query["hide_vflags"] = "4294967296";
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

        public async Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids)
        {
            var uriBuilder = new UriBuilder("https://www.wildberries.ru/__internal/u-card/cards/v4/detail");

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["appType"] = "1";
            query["curr"] = "rub";
            query["dest"] = "-1257786";
            query["spp"] = "30";
            query["hide_vflags"] = "4294967296";
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

        private async Task<string> SendAuthorizedGetAsync(string url)
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
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyRequestHeaders(request, auth);

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

        private static void ApplyRequestHeaders(HttpRequestMessage request, WbScrapingAuthState auth)
        {
            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation(
                "accept-language",
                "ru,en-US;q=0.9,en;q=0.8,zh-CN;q=0.7,zh;q=0.6,de;q=0.5,es;q=0.4");
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {auth.AccessToken}");
            request.Headers.TryAddWithoutValidation("deviceid", auth.DeviceId);
            request.Headers.TryAddWithoutValidation("priority", "u=1, i");
            request.Headers.TryAddWithoutValidation(
                "referer",
                "https://www.wildberries.ru/");
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
                        IdInMarket = product.id,
                        Brand = product.brand,
                        FeedBacksCount = product.feedbacks,
                        Link = $"https://www.wildberries.ru/catalog/{productId}/detail.aspx",
                        Name = product.name,
                        Rating = product.rating,
                        ReviewRating = product.reviewRating,
                        ImageUrl = WbProductImageUrlBuilder.BuildBigImageUrl(product.id),
                        Category = string.IsNullOrWhiteSpace(product.entity)
                            ? null
                            : new WbCategory
                            {
                                Name = product.entity
                            },
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
