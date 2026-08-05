using Newtonsoft.Json;

namespace WildBerriesAnalyzer.Business.Models
{
    /// <summary>
    /// Ответ GET wbx-api-gateway.../share-basket/api/v1/basket/{shareId}
    /// </summary>
    public sealed class WbShareBasketResponse
    {
        [JsonProperty("items")]
        public List<WbShareBasketItem>? Items { get; set; }
    }

    public sealed class WbShareBasketItem
    {
        /// <summary>Артикул (nm).</summary>
        [JsonProperty("nmId")]
        public long NmId { get; set; }

        [JsonProperty("chrtId")]
        public long ChrtId { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }
}
