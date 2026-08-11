using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Parsing;

public static class OzonProductMapper
{
    public static WbProduct ToWbProduct(ParsedSearchItem item)
    {
        return new WbProduct
        {
            MarketType = MarketType.Ozon,
            IdInMarket = item.Sku,
            Name = item.Name ?? string.Empty,
            Brand = item.Brand ?? string.Empty,
            Rating = item.Rating ?? 0,
            ReviewRating = item.Rating ?? 0,
            FeedBacksCount = item.Reviews ?? 0,
            IsAdult = item.IsAdult,
            ImageUrl = item.ImageUrl ?? string.Empty,
            Link = item.Url ?? $"https://www.ozon.ru/product/{item.Sku}/",
            PriceFromInit = new WbPrice
            {
                CheckTime = DateTime.UtcNow,
                Price = item.Price
            }
        };
    }

    public static WbProduct ToWbProduct(ParsedProductDetails details)
    {
        WbCategory? category = null;
        if (!string.IsNullOrWhiteSpace(details.CategoryName) || details.CategoryId is not null)
        {
            category = new WbCategory
            {
                Id = details.CategoryId ?? 0,
                Name = details.CategoryName ?? string.Empty
            };
        }

        return new WbProduct
        {
            MarketType = MarketType.Ozon,
            IdInMarket = details.Sku,
            Name = details.Name ?? string.Empty,
            Brand = details.Brand ?? string.Empty,
            CategoryId = details.CategoryId,
            Category = category,
            Rating = details.Rating ?? 0,
            ReviewRating = details.Rating ?? 0,
            FeedBacksCount = details.Reviews ?? 0,
            IsAdult = false,
            ImageUrl = details.ImageUrl ?? string.Empty,
            Link = details.Url ?? (details.Sku > 0 ? $"https://www.ozon.ru/product/{details.Sku}/" : string.Empty),
            PriceFromInit = new WbPrice
            {
                CheckTime = DateTime.UtcNow,
                Price = details.Price
            }
        };
    }

    public static List<WbProduct> FromSearchPage(OzonComposerPage page, int limit = 36) =>
        OzonWidgetParser.ParseSearch(page, limit).Select(ToWbProduct).ToList();

    public static WbProduct FromProductPage(OzonComposerPage page) =>
        ToWbProduct(OzonWidgetParser.ParseDetails(page));
}
