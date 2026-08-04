using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Modules.MyFilters.Helpers
{
    public sealed class StrategyHelpInfo
    {
        public required string Title { get; init; }

        public required string Description { get; init; }

        public required string Example { get; init; }
    }

    public static class StrategyHelpCatalog
    {
        public static StrategyHelpInfo Get(ReferencePriceStrategy strategy) => strategy switch
        {
            ReferencePriceStrategy.LastKnownPrice => new StrategyHelpInfo
            {
                Title = "Последняя известная цена",
                Description =
                    "Референсной считается предыдущая зафиксированная цена товара — та, что была до текущей. " +
                    "Скидка = насколько текущая цена ниже прошлой проверки.",
                Example =
                    "История: 2 000 ₽ → 1 800 ₽ → 1 500 ₽ (сейчас).\n" +
                    "Референс: 1 800 ₽.\n" +
                    "Скидка: (1800 − 1500) / 1800 ≈ 17%."
            },
            ReferencePriceStrategy.AveragePrice => new StrategyHelpInfo
            {
                Title = "Средняя цена",
                Description =
                    "Референс — среднее арифметическое всех положительных цен в истории товара. " +
                    "Подходит, чтобы ловить снижение относительно «обычной» цены за всё время наблюдений.",
                Example =
                    "История: 1 000 ₽, 2 000 ₽, 3 000 ₽; сейчас 1 500 ₽.\n" +
                    "Референс: (1000 + 2000 + 3000) / 3 = 2 000 ₽.\n" +
                    "Скидка: (2000 − 1500) / 2000 = 25%."
            },
            ReferencePriceStrategy.Median => new StrategyHelpInfo
            {
                Title = "Медианная цена",
                Description =
                    "Референс — медиана всех положительных цен в истории. " +
                    "Устойчивее к редким скачкам и аномально высоким/низким значениям, чем среднее.",
                Example =
                    "История: 1 000 ₽, 2 000 ₽, 9 000 ₽; сейчас 1 500 ₽.\n" +
                    "Референс (медиана): 2 000 ₽.\n" +
                    "Скидка: (2000 − 1500) / 2000 = 25%.\n" +
                    "(Среднее было бы 4 000 ₽ — из‑за выброса 9 000 ₽.)"
            },
            ReferencePriceStrategy.MinimumHistorical => new StrategyHelpInfo
            {
                Title = "Минимальная за всё время",
                Description =
                    "Референс — самая низкая цена из всей истории наблюдений. " +
                    "Скидка есть только если текущая цена ниже исторического минимума " +
                    "(или близка к нему — в зависимости от вашего порога %).",
                Example =
                    "История: 2 500 ₽, 2 000 ₽, 1 800 ₽; сейчас 1 600 ₽.\n" +
                    "Референс: 1 800 ₽ (минимум).\n" +
                    "Скидка: (1800 − 1600) / 1800 ≈ 11%."
            },
            ReferencePriceStrategy.LowestPriceForLast30Days => new StrategyHelpInfo
            {
                Title = "Минимальная за 30 дней",
                Description =
                    "Референс — минимальная цена за последние 30 дней. " +
                    "Учитывается только недавняя история, старые минимумы не мешают.",
                Example =
                    "За 30 дней: 2 200 ₽, 1 900 ₽, 2 100 ₽; сейчас 1 700 ₽.\n" +
                    "Референс: 1 900 ₽.\n" +
                    "Скидка: (1900 − 1700) / 1900 ≈ 11%."
            },
            ReferencePriceStrategy.AveragePriceForLast30Days => new StrategyHelpInfo
            {
                Title = "Средняя за 30 дней",
                Description =
                    "Референс — среднее арифметическое цен за последние 30 дней. " +
                    "Показывает снижение относительно недавнего «среднего чека» товара.",
                Example =
                    "За 30 дней: 1 800 ₽, 2 000 ₽, 2 200 ₽; сейчас 1 600 ₽.\n" +
                    "Референс: (1800 + 2000 + 2200) / 3 = 2 000 ₽.\n" +
                    "Скидка: (2000 − 1600) / 2000 = 20%."
            },
            ReferencePriceStrategy.MedianPriceForLast30Days => new StrategyHelpInfo
            {
                Title = "Медианная за 30 дней",
                Description =
                    "Референс — медиана цен за последние 30 дней. " +
                    "Компромисс между устойчивостью к выбросам и актуальностью недавнего периода.",
                Example =
                    "За 30 дней: 1 500 ₽, 2 000 ₽, 2 500 ₽; сейчас 1 600 ₽.\n" +
                    "Референс (медиана): 2 000 ₽.\n" +
                    "Скидка: (2000 − 1600) / 2000 = 20%."
            },
            _ => new StrategyHelpInfo
            {
                Title = "Стратегия",
                Description = "Описание недоступно.",
                Example = string.Empty
            }
        };
    }
}
