using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Business.Models;

public static class SearchHubEvents
{
    public const string Progress = "SearchProgress";
}

public enum SearchProgressStage
{
    Started,
    MarketStarted,
    MarketPage,
    MarketCompleted,
    Completed,
    Failed
}

public sealed class SearchProgress
{
    public SearchProgressStage Stage { get; set; }

    public string Message { get; set; } = string.Empty;

    public MarketType? Market { get; set; }

    public int FoundCount { get; set; }

    public int Page { get; set; }
}
