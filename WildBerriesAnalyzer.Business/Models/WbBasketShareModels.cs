using Newtonsoft.Json;

namespace WildBerriesAnalyzer.Business.Models
{
    /// <summary>
    /// Ответ POST __internal/basket-api/.../data_v2?shareId=…
    /// </summary>
    public sealed class WbBasketDataV2Response
    {
        [JsonProperty("resultState")]
        public int ResultState { get; set; }

        [JsonProperty("value")]
        public WbBasketDataV2Value? Value { get; set; }
    }

    public sealed class WbBasketDataV2Value
    {
        [JsonProperty("data")]
        public WbBasketDataV2Data? Data { get; set; }
    }

    public sealed class WbBasketDataV2Data
    {
        [JsonProperty("basket")]
        public WbBasketDataV2Basket? Basket { get; set; }
    }

    public sealed class WbBasketDataV2Basket
    {
        [JsonProperty("basketItems")]
        public List<WbBasketDataV2Item>? BasketItems { get; set; }
    }

    public sealed class WbBasketDataV2Item
    {
        /// <summary>Артикул (nm) товара.</summary>
        [JsonProperty("cod1S")]
        public long Cod1S { get; set; }

        [JsonProperty("goodsName")]
        public string? GoodsName { get; set; }
    }
}
