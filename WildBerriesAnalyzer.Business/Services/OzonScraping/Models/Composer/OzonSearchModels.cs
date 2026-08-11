using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

public sealed class OzonTileGridWidget
{
    [JsonPropertyName("items")]
    public List<OzonSearchTileItem>? Items { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }
}

public sealed class OzonInfiniteVirtualPaginatorWidget
{
    [JsonPropertyName("nextPage")]
    public string? NextPage { get; set; }

    [JsonPropertyName("prevPage")]
    public string? PrevPage { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("layoutContainer")]
    public string? LayoutContainer { get; set; }
}

public sealed class OzonSearchTileItem
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("sku")]
    public long? Sku { get; set; }

    [JsonPropertyName("isAdult")]
    public bool IsAdult { get; set; }

    [JsonPropertyName("action")]
    public OzonTileAction? Action { get; set; }

    [JsonPropertyName("mainState")]
    public List<OzonMainStateBlock>? MainState { get; set; }

    [JsonPropertyName("tileImage")]
    public OzonTileImage? TileImage { get; set; }
}

public sealed class OzonTileAction
{
    [JsonPropertyName("link")]
    public string? Link { get; set; }
}

public sealed class OzonTileImage
{
    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("items")]
    public List<OzonTileImageItem>? Items { get; set; }
}

public sealed class OzonTileImageItem
{
    [JsonPropertyName("image")]
    public OzonImageLink? Image { get; set; }
}

public sealed class OzonImageLink
{
    [JsonPropertyName("link")]
    public string? Link { get; set; }
}

public sealed class OzonMainStateBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("priceV2")]
    public OzonPriceV2Block? PriceV2 { get; set; }

    [JsonPropertyName("textDS")]
    public OzonTextDs? TextDs { get; set; }

    [JsonPropertyName("labelListV2")]
    public OzonLabelListV2? LabelListV2 { get; set; }
}

public sealed class OzonPriceV2Block
{
    [JsonPropertyName("price")]
    public List<OzonPriceText>? Price { get; set; }

    [JsonPropertyName("discount")]
    public string? Discount { get; set; }
}

public sealed class OzonPriceText
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("textStyle")]
    public string? TextStyle { get; set; }
}

public sealed class OzonTextDs
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class OzonLabelListV2
{
    [JsonPropertyName("items")]
    public List<OzonLabelListItem>? Items { get; set; }
}

public sealed class OzonLabelListItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public OzonTextDs? Text { get; set; }

    [JsonPropertyName("icon")]
    public OzonLabelIconWrap? Icon { get; set; }
}

public sealed class OzonLabelIconWrap
{
    [JsonPropertyName("icon")]
    public OzonLabelIcon? Icon { get; set; }
}

public sealed class OzonLabelIcon
{
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}
