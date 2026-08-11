using System.Text.Json;
using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

/// <summary>
/// Корневой ответ composer-api.bx/page/json/v2.
/// Значения widgetStates — JSON-строки виджетов.
/// </summary>
public sealed class OzonComposerPage
{
    [JsonPropertyName("widgetStates")]
    public Dictionary<string, string>? WidgetStates { get; set; }

    [JsonPropertyName("seo")]
    public OzonSeo? Seo { get; set; }

    /// <summary>
    /// Иногда приходит строкой JSON, иногда объектом.
    /// </summary>
    [JsonPropertyName("layoutTrackingInfo")]
    public JsonElement? LayoutTrackingInfo { get; set; }

    [JsonPropertyName("pageToken")]
    public string? PageToken { get; set; }

    [JsonPropertyName("nextPage")]
    public string? NextPage { get; set; }
}

public sealed class OzonSeo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("link")]
    public List<OzonSeoLink>? Link { get; set; }
}

public sealed class OzonSeoLink
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("rel")]
    public string? Rel { get; set; }
}

public sealed class OzonLayoutTrackingInfo
{
    [JsonPropertyName("sku")]
    public long? Sku { get; set; }

    [JsonPropertyName("categoryId")]
    public int? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("currentPageUrl")]
    public string? CurrentPageUrl { get; set; }

    [JsonPropertyName("pageType")]
    public string? PageType { get; set; }
}
