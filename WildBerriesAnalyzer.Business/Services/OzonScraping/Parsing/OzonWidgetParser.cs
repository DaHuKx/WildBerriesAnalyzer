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

    private static readonly Regex CartSkuJsonRegex = new(
        @"""(?:sku|skuId|offerId)""\s*:\s*""?(\d{8,16})""?",
        RegexOptions.Compiled);

    private static readonly Regex CartProductIdJsonRegex = new(
        @"""product_?[Ii]d""\s*:\s*""?(\d{8,16})""?",
        RegexOptions.Compiled);

    private static readonly Regex CartItemIdJsonRegex = new(
        @"""itemId""\s*:\s*""?(\d{8,16})""?",
        RegexOptions.Compiled);

    private static readonly Regex CartProductUrlRegex = new(
        @"/product/(?:[^""\\/?#]*-)?(\d{8,16})/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] CartShareWidgetNameHints =
    [
        "cartSplit",
        "webCart",
        "cartTile",
        "splitCart",
        "sharedCart",
        "cartShare",
        "cartLine",
        "cartItem",
        "cartProduct",
        "shareCart",
        "shared",
        "basket",
        "rfbsSplit"
    ];

    private static readonly string[] CartShareExcludedWidgetNameHints =
    [
        "tileGrid",
        "searchResult",
        "searchResults",
        "infiniteVirtual",
        "skuShelf",
        "skuLine",
        "skuGrid",
        "skuCarousel",
        "recommend",
        "analog",
        "viewed",
        "favorite",
        "history",
        "paginator",
        "webSale",
        "webReview",
        "webGallery",
        "webPrice",
        "webProductHeading",
        "webSingleProductScore",
        "banner",
        "marketing",
        "stories",
        "catalogMenu",
        "horizontalMenu",
        "verticalMenu",
        "megaMenu",
        "iconMenu",
        "sidebarMenu",
        "tabBar",
        "navBar"
    ];

    /// <summary>Исключаем только точное имя виджета (не substring — иначе режется cartHeader).</summary>
    private static readonly string[] CartShareExcludedExactWidgetNames =
    [
        "header",
        "footer",
        "cookie"
    ];

    private const int CartShareMaxReasonableItems = 50;

    /// <summary>
    /// SKU товаров из страницы общей корзины Ozon.
    /// Blacklist рекомендаций + выбор «коротких» cart-виджетов (shared cart обычно меньше полок).
    /// </summary>
    public static List<long> ParseCartShareSkus(OzonComposerPage? page) =>
        SelectCartShareSkus(CollectCartShareCandidates(page));

    /// <summary>Для отладки: все виджеты с числом SKU (включая blacklist).</summary>
    public static IReadOnlyList<(string WidgetKey, int Count)> DebugAllCartShareWidgets(OzonComposerPage? page)
    {
        if (page?.WidgetStates is null || page.WidgetStates.Count == 0)
        {
            return [];
        }

        var result = new List<(string, int)>();
        foreach (var (key, raw) in page.WidgetStates)
        {
            var skus = new HashSet<long>();
            CollectCartShareSkusStructured(raw, skus);
            CollectCartShareSkusRegexFallback(raw, skus);
            if (skus.Count > 0)
            {
                result.Add((key, skus.Count));
            }
        }

        return result.OrderByDescending(x => x.Item2).ToList();
    }

    /// <summary>Для отладки: SKU по каждому не-рекламному виджету.</summary>
    public static IReadOnlyList<(string WidgetKey, int Count)> DebugCartShareWidgets(OzonComposerPage? page) =>
        CollectCartShareCandidates(page)
            .Select(c => (c.WidgetKey, c.Skus.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

    /// <summary>Тот же отбор SKU, что и для composer, но для DOM-buckets.</summary>
    public static List<long> SelectCartShareSkusFromBuckets(
        IReadOnlyList<(string WidgetKey, IReadOnlyCollection<long> Skus)> buckets)
    {
        if (buckets.Count == 0)
        {
            return [];
        }

        var candidates = buckets
            .Select(b => new CartShareWidgetCandidate(
                b.WidgetKey,
                b.Skus.Where(IsPlausibleOzonSku).ToHashSet()))
            .Where(c => c.Skus.Count > 0)
            .ToList();

        return SelectCartShareSkus(candidates);
    }

    private sealed record CartShareWidgetCandidate(string WidgetKey, HashSet<long> Skus);

    private static List<CartShareWidgetCandidate> CollectCartShareCandidates(OzonComposerPage? page)
    {
        if (page?.WidgetStates is null || page.WidgetStates.Count == 0)
        {
            return [];
        }

        var candidates = new List<CartShareWidgetCandidate>();

        foreach (var (key, raw) in page.WidgetStates)
        {
            if (IsCartShareExcludedWidget(WidgetName(key)))
            {
                continue;
            }

            var widgetSkus = new HashSet<long>();
            CollectCartShareSkusStructured(raw, widgetSkus);
            CollectCartShareSkusRegexFallback(raw, widgetSkus);

            if (widgetSkus.Count > 0)
            {
                candidates.Add(new CartShareWidgetCandidate(key, widgetSkus));
            }
        }

        return candidates;
    }

    private static List<long> SelectCartShareSkus(IReadOnlyList<CartShareWidgetCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        // Только cart/share-виджеты. Иначе chrome вроде horizontalMenu даёт ложные SKU,
        // и навигационный fallback (DOM / action API) не запускается.
        var preferred = candidates
            .Where(c =>
                c.Skus.Count is >= 1 and <= CartShareMaxReasonableItems &&
                (IsCartShareHintWidget(c.WidgetKey) ||
                 IsStrongCartShareWidget(c.WidgetKey) ||
                 c.WidgetKey.Contains("share", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (preferred.Count == 0)
        {
            return [];
        }

        var hint = preferred.Where(c => IsCartShareHintWidget(c.WidgetKey)).ToList();
        if (hint.Count > 0)
        {
            var mergedHint = MergeCandidateSkus(hint);
            if (mergedHint.Count is > 0 and <= CartShareMaxReasonableItems)
            {
                return mergedHint;
            }
        }

        var share = preferred
            .Where(c => c.WidgetKey.Contains("share", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (share.Count > 0)
        {
            var mergedShare = MergeCandidateSkus(share);
            if (mergedShare.Count is > 0 and <= CartShareMaxReasonableItems)
            {
                return mergedShare;
            }
        }

        var strong = preferred.Where(c => IsStrongCartShareWidget(c.WidgetKey)).ToList();
        if (strong.Count > 0)
        {
            var mergedStrong = MergeCandidateSkus(strong);
            if (mergedStrong.Count is > 0 and <= CartShareMaxReasonableItems)
            {
                return mergedStrong;
            }
        }

        return [];
    }

    private static bool IsStrongCartShareWidget(string widgetKey)
    {
        if (string.IsNullOrWhiteSpace(widgetKey))
        {
            return false;
        }

        ReadOnlySpan<string> strong =
        [
            "cartSplit",
            "webCart",
            "sharedCart",
            "cartShare",
            "shareCart",
            "splitCart",
            "rfbsSplit"
        ];

        var name = WidgetName(widgetKey);
        foreach (var hint in strong)
        {
            if (name.StartsWith(hint, StringComparison.OrdinalIgnoreCase) ||
                widgetKey.StartsWith(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<long> MergeCandidateSkus(IEnumerable<CartShareWidgetCandidate> candidates)
    {
        var merged = new HashSet<long>();
        foreach (var candidate in candidates)
        {
            merged.UnionWith(candidate.Skus);
        }

        return merged.OrderBy(x => x).ToList();
    }

    private static bool IsCartShareHintWidget(string widgetKey)
    {
        if (string.IsNullOrWhiteSpace(widgetKey))
        {
            return false;
        }

        var name = WidgetName(widgetKey);
        if (IsCartShareExcludedWidget(name))
        {
            return false;
        }

        if (CartShareWidgetNameHints.Any(h =>
                name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
                widgetKey.Contains(h, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (name.Contains("cart", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("cartButton", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // split-123-default-1 без префикса cart
        return name.Equals("split", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("split", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCartShareExcludedWidget(string widgetName)
    {
        foreach (var exact in CartShareExcludedExactWidgetNames)
        {
            if (widgetName.Equals(exact, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (widgetName.Contains("Menu", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var hint in CartShareExcludedWidgetNameHints)
        {
            if (widgetName.StartsWith(hint, StringComparison.OrdinalIgnoreCase) ||
                widgetName.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectCartShareSkusStructured(string? raw, HashSet<long> skus)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            CollectCartShareSkusFromElement(doc.RootElement, skus, depth: 0);
        }
        catch
        {
            // ignore broken widget JSON
        }
    }

    private static void CollectCartShareSkusRegexFallback(string? raw, HashSet<long> skus)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (Match match in CartProductUrlRegex.Matches(raw))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && IsPlausibleOzonSku(sku))
            {
                skus.Add(sku);
            }
        }

        foreach (Match match in CartSkuJsonRegex.Matches(raw))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && IsPlausibleOzonSku(sku))
            {
                skus.Add(sku);
            }
        }

        foreach (Match match in CartProductIdJsonRegex.Matches(raw))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && IsPlausibleOzonSku(sku))
            {
                skus.Add(sku);
            }
        }

        foreach (Match match in CartItemIdJsonRegex.Matches(raw))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && IsPlausibleOzonSku(sku))
            {
                skus.Add(sku);
            }
        }
    }

    private static void CollectCartShareSkusFromElement(JsonElement element, HashSet<long> skus, int depth)
    {
        if (depth > 8)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (LooksLikeCartLineItem(element))
                {
                    var sku = TryGetSkuFromCartItem(element);
                    if (sku > 0)
                    {
                        skus.Add(sku);
                        return;
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    if (IsCartItemsPropertyName(prop.Name))
                    {
                        CollectSkusFromItemsArray(prop.Value, skus);
                        continue;
                    }

                    CollectCartShareSkusFromElement(prop.Value, skus, depth + 1);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectCartShareSkusFromElement(item, skus, depth + 1);
                }

                break;
        }
    }

    private static bool IsCartItemsPropertyName(string name) =>
        name.Equals("items", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("cartItems", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("lineItems", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("products", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("splits", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sections", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("goods", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("orderItems", StringComparison.OrdinalIgnoreCase);

    private static void CollectSkusFromItemsArray(JsonElement array, HashSet<long> skus)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                string.Equals(item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null,
                    "split", StringComparison.OrdinalIgnoreCase) &&
                item.TryGetProperty("items", out var nestedItems))
            {
                CollectSkusFromItemsArray(nestedItems, skus);
                continue;
            }

            var sku = TryGetSkuFromCartItem(item);
            if (sku > 0)
            {
                skus.Add(sku);
            }
        }
    }

    private static bool LooksLikeCartLineItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (item.TryGetProperty("sku", out _) ||
            item.TryGetProperty("skuId", out _) ||
            item.TryGetProperty("offerId", out _) ||
            item.TryGetProperty("productId", out _) ||
            item.TryGetProperty("product_id", out _) ||
            item.TryGetProperty("itemId", out _))
        {
            return true;
        }

        if (item.TryGetProperty("id", out _) &&
            (item.TryGetProperty("quantity", out _) ||
             item.TryGetProperty("qty", out _) ||
             item.TryGetProperty("count", out _) ||
             item.TryGetProperty("title", out _) ||
             item.TryGetProperty("name", out _) ||
             item.TryGetProperty("price", out _) ||
             item.TryGetProperty("product", out _)))
        {
            return true;
        }

        if (item.TryGetProperty("action", out var action) &&
            action.TryGetProperty("link", out var link) &&
            link.GetString()?.Contains("/product/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return item.TryGetProperty("link", out var directLink) &&
               directLink.GetString()?.Contains("/product/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static long TryGetSkuFromCartItem(JsonElement item)
    {
        foreach (var name in new[] { "sku", "skuId", "offerId", "productId", "product_id", "itemId" })
        {
            if (!item.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (TryReadPlausibleSku(prop, out var sku))
            {
                return sku;
            }
        }

        if (item.TryGetProperty("id", out var idProp) && TryReadPlausibleSku(idProp, out var idSku) &&
            (item.TryGetProperty("link", out _) ||
             item.TryGetProperty("action", out _) ||
             item.TryGetProperty("product", out _) ||
             item.TryGetProperty("quantity", out _) ||
             item.TryGetProperty("qty", out _) ||
             item.TryGetProperty("title", out _) ||
             item.TryGetProperty("name", out _) ||
             item.TryGetProperty("price", out _)))
        {
            return idSku;
        }

        if (item.TryGetProperty("product", out var product))
        {
            foreach (var name in new[] { "sku", "skuId", "id", "productId", "product_id" })
            {
                if (product.TryGetProperty(name, out var prop) &&
                    TryReadPlausibleSku(prop, out var sku))
                {
                    return sku;
                }
            }
        }

        if (item.TryGetProperty("cellTrackingInfo", out var tracking))
        {
            if (tracking.TryGetProperty("product", out var trackedProduct) &&
                trackedProduct.TryGetProperty("id", out var trackedId) &&
                TryReadPlausibleSku(trackedId, out var trackedSku))
            {
                return trackedSku;
            }

            if (tracking.TryGetProperty("sku", out var trackedSkuProp) &&
                TryReadPlausibleSku(trackedSkuProp, out var trackingSku))
            {
                return trackingSku;
            }
        }

        if (item.TryGetProperty("action", out var action) &&
            action.TryGetProperty("link", out var linkProp))
        {
            var fromLink = SkuFromUrl(linkProp.GetString());
            if (fromLink is > 0)
            {
                return fromLink.Value;
            }
        }

        if (item.TryGetProperty("link", out var directLink))
        {
            var fromLink = SkuFromUrl(directLink.GetString());
            if (fromLink is > 0)
            {
                return fromLink.Value;
            }
        }

        return 0;
    }

    private static bool TryReadPlausibleSku(JsonElement prop, out long sku) =>
        TryReadPlausibleSku(prop, out sku, depth: 0);

    private static bool TryReadPlausibleSku(JsonElement prop, out long sku, int depth)
    {
        sku = 0;
        if (depth > 3)
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out sku))
        {
            return IsPlausibleOzonSku(sku);
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            var text = prop.GetString();
            if (long.TryParse(text, out sku))
            {
                return IsPlausibleOzonSku(sku);
            }

            var fromUrl = SkuFromUrl(text);
            if (fromUrl is > 0)
            {
                sku = fromUrl.Value;
                return IsPlausibleOzonSku(sku);
            }

            return false;
        }

        if (prop.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "id", "sku", "skuId", "productId", "product_id", "value" })
            {
                if (prop.TryGetProperty(name, out var nested) &&
                    TryReadPlausibleSku(nested, out sku, depth + 1))
                {
                    return true;
                }
            }
        }

        sku = 0;
        return false;
    }

    private static bool IsPlausibleOzonSku(long sku) =>
        sku is >= 10_000_000 and <= 999_999_999_999_999;
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
