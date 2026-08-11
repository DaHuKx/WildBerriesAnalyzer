using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Parsing;

public static class OzonWidgetParser
{
    private static readonly Regex DigitsRegex = new(@"[^\d]", RegexOptions.Compiled);
    private static readonly Regex SkuFromUrlRegex = new(@"-(\d+)\/?(?:\?|$)", RegexOptions.Compiled);
    private static readonly Regex SkuLooseRegex = new(@"(\d{6,})", RegexOptions.Compiled);
    private static readonly Regex RatingInTextRegex = new(@"(\d[.,]\d)", RegexOptions.Compiled);
    private static readonly Regex ReviewsInTextRegex = new(@"(\d[\d\s]*)\s*отзыв", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SkuButtonRegex = new(@"(\d{6,})", RegexOptions.Compiled);

    private static readonly Regex BadgeRegex = new(
        @"^(стало дешевле|оригинал|хит|новинка|акция|распродажа|выбор|бестселлер|ozon|premium|самовывоз|скидка|бренд проверен|проверено|восстановленный)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string WidgetName(string key)
    {
        var idx = key.IndexOf('-');
        return idx < 0 ? key : key[..idx];
    }

    public static T? GetWidget<T>(OzonComposerPage? page, string name) where T : class
    {
        if (page?.WidgetStates is null)
        {
            return null;
        }

        foreach (var (key, raw) in page.WidgetStates)
        {
            if (!string.Equals(WidgetName(key), name, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return OzonJson.Deserialize<T>(raw);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static IEnumerable<T> GetWidgets<T>(OzonComposerPage? page, string name) where T : class
    {
        if (page?.WidgetStates is null)
        {
            yield break;
        }

        foreach (var (key, raw) in page.WidgetStates)
        {
            if (!string.Equals(WidgetName(key), name, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            T? parsed = null;
            try
            {
                parsed = OzonJson.Deserialize<T>(raw);
            }
            catch
            {
                // ignore broken widget
            }

            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    public static decimal? PriceToNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = DigitsRegex.Replace(text, string.Empty);
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static string? CleanUrl(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var path = link.Split('?', 2)[0];
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return "https://www.ozon.ru" + path;
    }

    public static long? SkuFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var m = SkuFromUrlRegex.Match(url);
        if (m.Success && long.TryParse(m.Groups[1].Value, out var sku))
        {
            return sku;
        }

        m = SkuLooseRegex.Match(url);
        if (m.Success && long.TryParse(m.Groups[1].Value, out sku))
        {
            return sku;
        }

        return null;
    }

    public static OzonLayoutTrackingInfo? ParseLayoutTracking(OzonComposerPage page)
    {
        if (page.LayoutTrackingInfo is null ||
            page.LayoutTrackingInfo.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        try
        {
            var el = page.LayoutTrackingInfo.Value;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : OzonJson.Deserialize<OzonLayoutTrackingInfo>(s);
            }

            return el.Deserialize<OzonLayoutTrackingInfo>(OzonJson.Options);
        }
        catch
        {
            return null;
        }
    }

    public static (double? Rating, int? Reviews) ParseScoreText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        double? rating = null;
        int? reviews = null;

        var rm = RatingInTextRegex.Match(text);
        if (rm.Success)
        {
            rating = double.Parse(rm.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        var cm = ReviewsInTextRegex.Match(text);
        if (cm.Success)
        {
            reviews = (int?)PriceToNumber(cm.Groups[1].Value);
        }

        return (rating, reviews);
    }

    public static ParsedSearchItem? ParseSearchItem(OzonSearchTileItem? item)
    {
        if (item is null)
        {
            return null;
        }

        var ms = item.MainState ?? new List<OzonMainStateBlock>();

        var priceBlock = ms.FirstOrDefault(s => string.Equals(s.Type, "priceV2", StringComparison.Ordinal))?.PriceV2;
        var prices = priceBlock?.Price ?? new List<OzonPriceText>();
        var price = PriceToNumber(prices.FirstOrDefault(p => p.TextStyle == "PRICE")?.Text);
        var oldPrice = PriceToNumber(prices.FirstOrDefault(p => p.TextStyle == "ORIGINAL_PRICE")?.Text);

        var name = ms.FirstOrDefault(s => s.Id == "name")?.TextDs?.Text
                   ?? ms.FirstOrDefault(s => string.Equals(s.Type, "textDS", StringComparison.Ordinal)
                                             && !string.IsNullOrWhiteSpace(s.TextDs?.Text)
                                             && s.Id != "name"
                                             && (s.TextDs!.Text!.Length > 20))
                       ?.TextDs?.Text;

        double? rating = null;
        int? reviews = null;
        var ratingBlock = ms.FirstOrDefault(s =>
            s.LabelListV2?.Items is not null &&
            s.LabelListV2.Items.Any(i =>
                i.Icon?.Icon?.Icon is not null &&
                i.Icon.Icon.Icon.Contains("star", StringComparison.OrdinalIgnoreCase)));

        if (ratingBlock?.LabelListV2?.Items is not null)
        {
            var texts = ratingBlock.LabelListV2.Items
                .Where(x => x.Type == "text")
                .Select(x => x.Text?.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Cast<string>()
                .ToList();

            if (texts.Count > 0 &&
                double.TryParse(texts[0].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
            {
                rating = r;
            }

            if (texts.Count > 1)
            {
                reviews = (int?)PriceToNumber(texts[1]);
            }
        }

        string? brand = null;
        foreach (var block in ms.Where(s => s.LabelListV2 is not null))
        {
            if (block.LabelListV2!.Items?.Any(i =>
                    i.Icon?.Icon?.Icon is not null &&
                    i.Icon.Icon.Icon.Contains("star", StringComparison.OrdinalIgnoreCase)) == true)
            {
                continue;
            }

            var cand = block.LabelListV2.Items?
                .FirstOrDefault(x => x.Type == "text")?.Text?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(cand) && !BadgeRegex.IsMatch(cand))
            {
                brand = cand;
                break;
            }
        }

        // Often brand is embedded after marketing prefix: "Восстановленный Apple Смартфон..."
        if (string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(name))
        {
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < Math.Min(tokens.Length, 4); i++)
            {
                if (BadgeRegex.IsMatch(tokens[i]))
                {
                    continue;
                }

                if (tokens[i].Length is >= 2 and <= 40)
                {
                    brand = tokens[i];
                    break;
                }
            }
        }

        var url = CleanUrl(item.Action?.Link);
        var sku = item.Sku ?? item.Id ?? SkuFromUrl(url);
        var image =
            item.TileImage?.Items?.Select(x => x.Image?.Link).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? item.TileImage?.CoverImage;

        if (sku is null or <= 0)
        {
            return null;
        }

        return new ParsedSearchItem
        {
            Sku = sku.Value,
            Name = name,
            Brand = brand,
            Price = price ?? 0,
            OldPrice = oldPrice is > 0 && oldPrice > price ? oldPrice : null,
            Rating = rating,
            Reviews = reviews,
            Url = url ?? $"https://www.ozon.ru/product/{sku}/",
            ImageUrl = image,
            IsAdult = item.IsAdult
        };
    }

    public static List<ParsedSearchItem> ParseSearch(OzonComposerPage page, int limit = int.MaxValue)
    {
        // На одной странице поиска Ozon обычно ~8 товаров в tileGridDesktop.
        var items = GetWidgets<OzonTileGridWidget>(page, "tileGridDesktop")
            .SelectMany(g => g.Items ?? Enumerable.Empty<OzonSearchTileItem>())
            .Select(ParseSearchItem)
            .Where(x => x is not null)
            .Cast<ParsedSearchItem>()
            .GroupBy(x => x.Sku)
            .Select(g => g.First())
            .Take(limit)
            .ToList();

        return items;
    }

    /// <summary>
    /// Путь следующей страницы поиска из root.nextPage или infiniteVirtualPaginator.nextPage.
    /// </summary>
    public static string? GetNextSearchPagePath(OzonComposerPage page)
    {
        if (!string.IsNullOrWhiteSpace(page.NextPage))
        {
            return NormalizeNextPath(page.NextPage);
        }

        var paginator = GetWidget<OzonInfiniteVirtualPaginatorWidget>(page, "infiniteVirtualPaginator");
        if (!string.IsNullOrWhiteSpace(paginator?.NextPage))
        {
            return NormalizeNextPath(paginator.NextPage);
        }

        return null;
    }

    private static string NormalizeNextPath(string next)
    {
        next = next.Trim();
        if (next.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(next);
            return uri.PathAndQuery;
        }

        return next.StartsWith('/') ? next : "/" + next;
    }

    public static ParsedProductDetails ParseDetails(OzonComposerPage page)
    {
        var heading = GetWidget<OzonWebProductHeadingWidget>(page, "webProductHeading");
        var price = GetWidget<OzonWebPriceWidget>(page, "webPrice");
        var gallery = GetWidget<OzonWebGalleryWidget>(page, "webGallery");
        var brandWidget = GetWidget<OzonWebBrandWidget>(page, "webBrand");
        var reviewScore = GetWidget<OzonWebReviewProductScoreWidget>(page, "webReviewProductScore");
        var singleScore = GetWidget<OzonWebSingleProductScoreWidget>(page, "webSingleProductScore");
        var detailSku = GetWidget<OzonWebDetailSkuWidget>(page, "webDetailSKU");
        var sale = GetWidget<OzonWebSaleWidget>(page, "webSale");
        var layout = ParseLayoutTracking(page);

        long? sku =
            gallery?.Sku
            ?? layout?.Sku
            ?? reviewScore?.ItemId
            ?? sale?.CellTrackingInfo?.Product?.Id
            ?? SkuFromUrl(page.Seo?.Link?.FirstOrDefault(l => l.Rel == "canonical")?.Href)
            ?? SkuFromUrl(page.Seo?.Link?.FirstOrDefault()?.Href);

        if (sku is null && !string.IsNullOrWhiteSpace(detailSku?.Button?.Text))
        {
            var m = SkuButtonRegex.Match(detailSku.Button.Text);
            if (m.Success && long.TryParse(m.Groups[1].Value, out var fromButton))
            {
                sku = fromButton;
            }
        }

        var url =
            CleanUrl(page.Seo?.Link?.FirstOrDefault(l => l.Rel == "canonical")?.Href)
            ?? CleanUrl(layout?.CurrentPageUrl)
            ?? (sku is not null ? $"https://www.ozon.ru/product/{sku}/" : null);

        double? rating = reviewScore?.TotalScore ?? reviewScore?.Score;
        int? reviews = reviewScore?.ReviewsCount;
        if (rating is null || reviews is null)
        {
            var fromText = ParseScoreText(singleScore?.Text);
            rating ??= fromText.Rating;
            reviews ??= fromText.Reviews;
        }

        // Prefer витринная цена без карты; fallback на cardPrice / sale tracking.
        var priceRub =
            PriceToNumber(price?.Price)
            ?? PriceToNumber(price?.CardPrice)
            ?? sale?.CellTrackingInfo?.Product?.FinalPrice
            ?? sale?.CellTrackingInfo?.Product?.Price
            ?? 0;

        var brand =
            brandWidget?.Content?.Title?.Text?
                .Select(n => n.Content ?? n.Text)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
            ?? sale?.CellTrackingInfo?.Product?.Brand;

        var name =
            heading?.Title
            ?? sale?.CellTrackingInfo?.Product?.Title
            ?? page.Seo?.Title;

        if (string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(name))
        {
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens.Take(4))
            {
                if (BadgeRegex.IsMatch(token) || token.Length is < 2 or > 40)
                {
                    continue;
                }

                brand = token;
                break;
            }
        }

        var image = gallery?.CoverImage
                    ?? gallery?.Images?
                        .Select(i => i.Src ?? i.Image)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return new ParsedProductDetails
        {
            Sku = sku ?? 0,
            Name = name,
            Brand = brand,
            Url = url,
            ImageUrl = image,
            Price = priceRub,
            Rating = rating,
            Reviews = reviews,
            CategoryName = layout?.CategoryName,
            CategoryId = layout?.CategoryId,
            IsAvailable = price?.IsAvailable
        };
    }
}

public sealed class ParsedSearchItem
{
    public long Sku { get; init; }
    public string? Name { get; init; }
    public string? Brand { get; init; }
    public decimal Price { get; init; }
    public decimal? OldPrice { get; init; }
    public double? Rating { get; init; }
    public int? Reviews { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsAdult { get; init; }
}

public sealed class ParsedProductDetails
{
    public long Sku { get; init; }
    public string? Name { get; init; }
    public string? Brand { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public decimal Price { get; init; }
    public double? Rating { get; init; }
    public int? Reviews { get; init; }
    public string? CategoryName { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsAvailable { get; init; }
}
