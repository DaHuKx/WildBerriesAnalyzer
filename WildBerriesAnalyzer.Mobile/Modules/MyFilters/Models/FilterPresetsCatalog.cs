using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public static class FilterPresetsCatalog
    {
        public static IReadOnlyList<FilterPreset> All { get; } =
        [
            new FilterPreset
            {
                Id = "bag",
                Title = "Корзина",
                Summary = "Только товары из вашей корзины, порог по скидке.",
                Description =
                    "Подходит, если вы уже собрали список товаров и хотите получать уведомления о снижении цены по ним. " +
                    "Отзывы и рейтинг для товаров из корзины не проверяются — учитывается только минимальная скидка.",
                EffectsText =
                    "• Тип: моя корзина\n" +
                    "• Мин. скидка: 15%\n" +
                    "• Отзывы / рейтинг: не важны для корзины\n" +
                    "• Стратегии: все\n" +
                    "• Состав корзины не изменяется",
                DiscontMinPercent = 15,
                MinReviewsCount = 0,
                MinRating = 0,
                ProductsFilterType = ProductsFilterType.OwnBag,
                Strategies = null
            },
            new FilterPreset
            {
                Id = "strict",
                Title = "Строгий отбор",
                Summary = "Каталог: заметная скидка и проверенные товары.",
                Description =
                    "Для тех, кто не хочет шумных уведомлений. Скидка должна быть ощутимой, у товара — достаточный рейтинг и число отзывов. " +
                    "Охват — весь каталог (без ограничения корзиной или категориями).",
                EffectsText =
                    "• Тип: все товары\n" +
                    "• Мин. скидка: 25%\n" +
                    "• Мин. отзывов: 50\n" +
                    "• Мин. рейтинг: 4.5\n" +
                    "• Стратегии: все\n" +
                    "• Корзина не изменяется",
                DiscontMinPercent = 25,
                MinReviewsCount = 50,
                MinRating = 4.5f,
                ProductsFilterType = ProductsFilterType.None,
                Strategies = null
            },
            new FilterPreset
            {
                Id = "wide",
                Title = "Широкий охват",
                Summary = "Больше уведомлений: низкий порог скидки.",
                Description =
                    "Максимальный поток скидок по каталогу. Подходит для мониторинга рынка. " +
                    "Пороги по отзывам и рейтингу отключены — остаётся только минимальный процент скидки.",
                EffectsText =
                    "• Тип: все товары\n" +
                    "• Мин. скидка: 10%\n" +
                    "• Мин. отзывов: 0\n" +
                    "• Мин. рейтинг: 0\n" +
                    "• Стратегии: все\n" +
                    "• Корзина не изменяется",
                DiscontMinPercent = 10,
                MinReviewsCount = 0,
                MinRating = 0,
                ProductsFilterType = ProductsFilterType.None,
                Strategies = null
            },
            new FilterPreset
            {
                Id = "month",
                Title = "За последние 30 дней",
                Summary = "Скидки относительно цен за месяц.",
                Description =
                    "Стратегии базовой цены ограничены окном 30 дней: минимум, среднее и медиана за месяц. " +
                    "Удобно ловить недавние просадки, а не сравнение с историческим минимумом за всё время.",
                EffectsText =
                    "• Тип: все товары\n" +
                    "• Мин. скидка: 15%\n" +
                    "• Мин. отзывов: 0\n" +
                    "• Мин. рейтинг: 0\n" +
                    "• Стратегии: мин. / средняя / медиана за 30 дней\n" +
                    "• Корзина не изменяется",
                DiscontMinPercent = 15,
                MinReviewsCount = 0,
                MinRating = 0,
                ProductsFilterType = ProductsFilterType.None,
                Strategies =
                [
                    ReferencePriceStrategy.LowestPriceForLast30Days,
                    ReferencePriceStrategy.AveragePriceForLast30Days,
                    ReferencePriceStrategy.MedianPriceForLast30Days
                ]
            },
            new FilterPreset
            {
                Id = "quality",
                Title = "Топ по качеству",
                Summary = "Скидки на товары с высоким рейтингом и многими отзывами.",
                Description =
                    "Фокус на проверенных позициях: высокий рейтинг и много отзывов плюс заметная скидка. " +
                    "Меньше случайных новичков каталога, больше «безопасных» покупок.",
                EffectsText =
                    "• Тип: все товары\n" +
                    "• Мин. скидка: 20%\n" +
                    "• Мин. отзывов: 100\n" +
                    "• Мин. рейтинг: 4.7\n" +
                    "• Стратегии: все\n" +
                    "• Корзина не изменяется",
                DiscontMinPercent = 20,
                MinReviewsCount = 100,
                MinRating = 4.7f,
                ProductsFilterType = ProductsFilterType.None,
                Strategies = null
            }
        ];
    }
}
