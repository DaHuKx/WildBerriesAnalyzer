using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

public sealed class OzonWebPriceWidget
{
    [JsonPropertyName("isAvailable")]
    public bool? IsAvailable { get; set; }

    /// <summary>Цена с картой Ozon (часто ниже).</summary>
    [JsonPropertyName("cardPrice")]
    public string? CardPrice { get; set; }

    /// <summary>Цена без карты.</summary>
    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("originalPrice")]
    public string? OriginalPrice { get; set; }
}

public sealed class OzonWebProductHeadingWidget
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public sealed class OzonWebGalleryWidget
{
    [JsonPropertyName("sku")]
    public long? Sku { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("images")]
    public List<OzonGalleryImage>? Images { get; set; }
}

public sealed class OzonGalleryImage
{
    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class OzonWebSingleProductScoreWidget
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class OzonWebReviewProductScoreWidget
{
    [JsonPropertyName("totalScore")]
    public double? TotalScore { get; set; }

    [JsonPropertyName("reviewsCount")]
    public int? ReviewsCount { get; set; }

    [JsonPropertyName("itemId")]
    public long? ItemId { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }
}

public sealed class OzonWebBrandWidget
{
    [JsonPropertyName("content")]
    public OzonWebBrandContent? Content { get; set; }
}

public sealed class OzonWebBrandContent
{
    [JsonPropertyName("title")]
    public OzonRichTitle? Title { get; set; }
}

public sealed class OzonRichTitle
{
    [JsonPropertyName("text")]
    public List<OzonRichTextNode>? Text { get; set; }
}

public sealed class OzonRichTextNode
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class OzonWebDetailSkuWidget
{
    [JsonPropertyName("button")]
    public OzonDetailSkuButton? Button { get; set; }
}

public sealed class OzonDetailSkuButton
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>Альтернативный источник цены/метаданных в webSale.</summary>
public sealed class OzonWebSaleWidget
{
    [JsonPropertyName("cellTrackingInfo")]
    public OzonSaleTrackingInfo? CellTrackingInfo { get; set; }
}

public sealed class OzonSaleTrackingInfo
{
    [JsonPropertyName("product")]
    public OzonSaleProductInfo? Product { get; set; }
}

public sealed class OzonSaleProductInfo
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("finalPrice")]
    public decimal? FinalPrice { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }
}
