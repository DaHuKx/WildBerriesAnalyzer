using System.Text;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Bots.Helpers
{
    public static class BotMessageBuilder
    {
        public static string BuildUserFilter(WbFilter filter)
        {
            if (filter == null)
                return " Фильтр не задан";

            var sb = new StringBuilder();

            // Заголовок
            sb.AppendLine(" *ИНФОРМАЦИЯ О ФИЛЬТРЕ*");
            sb.AppendLine(new string('─', 40));

            // 🔷 БАЗОВЫЕ ПАРАМЕТРЫ
            sb.AppendLine(" *Базовые параметры:*");
            sb.AppendLine($"   💰 Мин. скидка: {filter.DiscontMinPercent}%");
            sb.AppendLine($"   💬 Мин. отзывов: {filter.MinReviewsCount}");
            sb.AppendLine($"   ⭐ Мин. рейтинг: {filter.MinRating}");
            sb.AppendLine();

            // 🔷 ФИЛЬТРАЦИЯ ТОВАРОВ
            sb.AppendLine("🔹 *Фильтрация товаров:*");

            switch (filter.ProductsFilterType)
            {
                case ProductsFilterType.None:
                    sb.AppendLine("   🔘 Тип: *Не применяется*");
                    break;

                case ProductsFilterType.OwnBag:
                    sb.AppendLine("   🛍️ Тип: *Собственная корзина*");

                    if (filter.BagProducts == null || !filter.BagProducts.Any())
                    {
                        sb.AppendLine("   📦 Товаров: *0* (корзина пуста)");
                    }
                    else
                    {
                        var products = filter.BagProducts.Select(b =>
                            b.Product != null
                                ? $"• {b.Product.Name} (арт. {b.Product.IdInMarket})"
                                : $"• ID товара: {b.ProductId}"
                        ).ToList();

                        sb.AppendLine($"   📦 Товаров: *{products.Count}*");
                        foreach (var product in products)
                        {
                            sb.AppendLine($"   {product}");
                        }
                    }
                    break;

                case ProductsFilterType.Categories_BlackList:
                    sb.AppendLine("   🚫 Тип: *Черный список категорий*");

                    if (filter.FilterCategories == null || !filter.FilterCategories.Any())
                    {
                        sb.AppendLine("   📂 Категорий: *0* (список пуст)");
                    }
                    else
                    {
                        var categories = filter.FilterCategories.Select(c =>
                            c.Category != null && !string.IsNullOrEmpty(c.Category.Name)
                                ? $"• {c.Category.Name} (ID: {c.CategoryId})"
                                : $"• ID категории: {c.CategoryId}"
                        ).ToList();

                        sb.AppendLine($"   📂 Категорий: *{categories.Count}*");
                        foreach (var category in categories)
                        {
                            sb.AppendLine($"   {category}");
                        }
                    }
                    break;

                case ProductsFilterType.Categories_WhiteList:
                    sb.AppendLine("   ✅ Тип: *Белый список категорий*");

                    if (filter.FilterCategories == null || !filter.FilterCategories.Any())
                    {
                        sb.AppendLine("   📂 Категорий: *0* (список пуст)");
                    }
                    else
                    {
                        var categories = filter.FilterCategories.Select(c =>
                            c.Category != null && !string.IsNullOrEmpty(c.Category.Name)
                                ? $"• {c.Category.Name} (ID: {c.CategoryId})"
                                : $"• ID категории: {c.CategoryId}"
                        ).ToList();

                        sb.AppendLine($"   📂 Категорий: *{categories.Count}*");
                        foreach (var category in categories)
                        {
                            sb.AppendLine($"   {category}");
                        }
                    }
                    break;
            }

            sb.AppendLine();

            // 🔷 СТРАТЕГИИ ЦЕНЫ
            sb.AppendLine("🔹 *Стратегии расчета цены:*");

            if (filter.ReferencePriceStrartegies != null && filter.ReferencePriceStrartegies.Any())
            {
                sb.AppendLine($"   📊 Выбрано стратегий: *{filter.ReferencePriceStrartegies.Count}*");

                foreach (var strategy in filter.ReferencePriceStrartegies)
                {
                    string emoji = strategy switch
                    {
                        ReferencePriceStrategy.LastKnownPrice => "",
                        ReferencePriceStrategy.AveragePrice => "📈",
                        ReferencePriceStrategy.Median => "📊",
                        ReferencePriceStrategy.MinimumHistorical => "📉",
                        ReferencePriceStrategy.LowestPriceForLast30Days => "🗓️",
                        ReferencePriceStrategy.AveragePriceForLast30Days => "📅",
                        ReferencePriceStrategy.MedianPriceForLast30Days => "📆",
                        _ => "•"
                    };

                    sb.AppendLine($"   {emoji} {ToReadableString(strategy)}");
                }
            }
            else
            {
                sb.AppendLine("   ⚠️ Стратегии не выбраны");
            }

            sb.AppendLine(new string('─', 40));

            return sb.ToString();
        }

        // Метод расширения для красивого отображения названий стратегий
        private static string ToReadableString(this ReferencePriceStrategy strategy)
        {
            return strategy switch
            {
                ReferencePriceStrategy.LastKnownPrice => "Последняя известная цена",
                ReferencePriceStrategy.AveragePrice => "Средняя цена",
                ReferencePriceStrategy.Median => "Медианная цена",
                ReferencePriceStrategy.MinimumHistorical => "Минимальная историческая цена",
                ReferencePriceStrategy.LowestPriceForLast30Days => "Низшая цена за 30 дней",
                ReferencePriceStrategy.AveragePriceForLast30Days => "Средняя цена за 30 дней",
                ReferencePriceStrategy.MedianPriceForLast30Days => "Медианная цена за 30 дней",
                _ => strategy.ToString()
            };
        }

        public static string BuildProductsMessage(List<WbProduct> products, string header, int maxLength = 4096)
        {
            if (products == null || products.Count == 0)
            {
                return $"{header}\n\nСписок пуст.";
            }

            var sb = new StringBuilder();
            sb.AppendLine(header);

            int addedCount = 0;
            int totalProducts = products.Count;

            for (int i = 0; i < totalProducts; i++)
            {
                var product = products[i];
                // Добавляем маркер • для красоты. Если имя null, ставим заглушку
                var line = $"• {product.Name ?? "Без названия"}";

                int remaining = totalProducts - (addedCount + 1);

                // Формируем суффикс, если товары не влезут
                string suffix = remaining > 0 ? $"\n\n... и ещё {remaining} {GetProductPlural(remaining)}" : "";

                // Длина переноса строки (\n), если это не первый добавляемый товар
                int newlineLength = (addedCount == 0) ? 0 : 1;

                // Проверяем, влезет ли текущая строка + суффикс в лимит
                if (sb.Length + newlineLength + line.Length + suffix.Length <= maxLength)
                {
                    if (addedCount > 0)
                        sb.AppendLine();

                    sb.Append(line);
                    addedCount++;
                }
                else
                {
                    // Не влезает. Добавляем суффикс, если он сам влезает, и прерываем цикл
                    if (remaining > 0 && sb.Length + suffix.Length <= maxLength)
                    {
                        sb.Append(suffix);
                    }
                    break;
                }
            }

            // Финальная страховка: если даже заголовок + суффикс длиннее лимита (крайне редкий случай)
            if (sb.Length > maxLength)
            {
                return sb.ToString(0, Math.Max(0, maxLength - 3)) + "...";
            }

            return sb.ToString();
        }

        /// <summary>
        /// Вспомогательный метод для правильного склонения слова "товар" (1 товар, 2 товара, 5 товаров)
        /// </summary>
        private static string GetProductPlural(int count)
        {
            int mod10 = count % 10;
            int mod100 = count % 100;

            if (mod10 == 1 && mod100 != 11) return "товар";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "товара";
            return "товаров";
        }
    }
}
