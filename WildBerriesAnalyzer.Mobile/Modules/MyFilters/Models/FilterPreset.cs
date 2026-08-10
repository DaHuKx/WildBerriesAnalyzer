using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public sealed class FilterPreset
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        /// <summary>Краткое описание в списке.</summary>
        public required string Summary { get; init; }

        /// <summary>Подробное описание на карточке.</summary>
        public required string Description { get; init; }

        /// <summary>Что изменится после выбора (для UI).</summary>
        public required string EffectsText { get; init; }

        public int DiscontMinPercent { get; init; }

        public int MinReviewsCount { get; init; }

        public float MinRating { get; init; }

        public ProductsFilterType ProductsFilterType { get; init; }

        /// <summary>null или пусто — все стратегии.</summary>
        public IReadOnlyList<ReferencePriceStrategy>? Strategies { get; init; }
    }
}
