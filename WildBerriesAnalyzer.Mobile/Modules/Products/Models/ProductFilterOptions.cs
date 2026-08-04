namespace WildBerriesAnalyzer.Modules.Products.Models
{
    public class ProductSortOption
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<ProductSortOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Без сортировки" },
            new() { Id = 1, Name = "По цене" },
            new() { Id = 2, Name = "По рейтингу" },
            new() { Id = 3, Name = "По количеству отзывов" },
            new() { Id = 4, Name = "По медиане цены" }
        ];
    }

    public class ProductRatingOption
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<ProductRatingOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Любой рейтинг" },
            new() { Id = 1, Name = "От 1★ и выше" },
            new() { Id = 2, Name = "От 2★ и выше" },
            new() { Id = 3, Name = "От 3★ и выше" },
            new() { Id = 4, Name = "От 4★ и выше" },
            new() { Id = 5, Name = "Только 5★" }
        ];
    }

    public class ProductFeedBackOption
    {
        public int Id { get; init; }

        public int Count { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<ProductFeedBackOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Любое число отзывов", Count = 0 },
            new() { Id = 1, Name = "От 1 отзыва", Count = 1 },
            new() { Id = 2, Name = "От 5 отзывов", Count = 5 },
            new() { Id = 3, Name = "От 10 отзывов", Count = 10 },
            new() { Id = 4, Name = "От 50 отзывов", Count = 50 },
            new() { Id = 5, Name = "От 100 отзывов", Count = 100 }
        ];
    }
}
