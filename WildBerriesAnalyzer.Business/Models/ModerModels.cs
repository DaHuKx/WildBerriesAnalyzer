namespace WildBerriesAnalyzer.Business.Models
{
    public sealed class ModerProductDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Brand { get; set; }

        public long IdInMarket { get; set; }

        public string MarketType { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public string? Link { get; set; }
    }

    public sealed class ModerCategoryDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class ModerAssignRequest
    {
        public int ProductId { get; set; }

        public List<int> CategoryIds { get; set; } = new();

        public List<string>? NewCategoryNames { get; set; }
    }

    public sealed class ModerBulkAssignRequest
    {
        public List<int> ProductIds { get; set; } = new();

        public List<int> CategoryIds { get; set; } = new();

        public List<string>? NewCategoryNames { get; set; }
    }

    public sealed class ModerBulkAssignResultDto
    {
        public int AssignedCount { get; set; }
    }

    public sealed class ModerQueueCountDto
    {
        public int Count { get; set; }
    }
}
