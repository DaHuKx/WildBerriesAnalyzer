namespace WildBerriesAnalyzer.Modules.ActualDiscounts.Models
{
    public sealed class DiscontSortOption
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<DiscontSortOption> CreateAll() =>
        [
            new() { Id = 0, Name = "По размеру скидки" },
            new() { Id = 1, Name = "По цене" },
            new() { Id = 2, Name = "По рейтингу" },
            new() { Id = 3, Name = "По отзывам" },
            new() { Id = 4, Name = "По названию" }
        ];
    }

    public sealed class DiscontPercentOption
    {
        public int Id { get; init; }

        public int MinPercent { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<DiscontPercentOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Любая скидка", MinPercent = 0 },
            new() { Id = 1, Name = "От 5%", MinPercent = 5 },
            new() { Id = 2, Name = "От 10%", MinPercent = 10 },
            new() { Id = 3, Name = "От 20%", MinPercent = 20 },
            new() { Id = 4, Name = "От 30%", MinPercent = 30 },
            new() { Id = 5, Name = "От 50%", MinPercent = 50 }
        ];
    }

    public sealed class DiscontRatingOption
    {
        public int Id { get; init; }

        public double MinRating { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<DiscontRatingOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Любой рейтинг", MinRating = 0 },
            new() { Id = 1, Name = "От 3★ и выше", MinRating = 3 },
            new() { Id = 2, Name = "От 4★ и выше", MinRating = 4 },
            new() { Id = 3, Name = "От 4.5★ и выше", MinRating = 4.5 },
            new() { Id = 4, Name = "Только 5★", MinRating = 5 }
        ];
    }

    public sealed class DiscontFeedBackOption
    {
        public int Id { get; init; }

        public int MinCount { get; init; }

        public string Name { get; init; } = string.Empty;

        public static List<DiscontFeedBackOption> CreateAll() =>
        [
            new() { Id = 0, Name = "Любое число отзывов", MinCount = 0 },
            new() { Id = 1, Name = "От 10 отзывов", MinCount = 10 },
            new() { Id = 2, Name = "От 50 отзывов", MinCount = 50 },
            new() { Id = 3, Name = "От 100 отзывов", MinCount = 100 },
            new() { Id = 4, Name = "От 500 отзывов", MinCount = 500 }
        ];
    }
}
