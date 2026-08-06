using System.Text;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Bots.Helpers
{
    public static class BotMessageBuilder
    {
        /// <summary>
        /// Текст «Мои текущие фильтры». Список товаров/категорий обрезается под лимит VK (4096).
        /// </summary>
        /// <param name="bagProducts">
        /// Товары корзины с именами (предпочтительно из GetUserBagProductsAsync).
        /// Если null — берём filter.BagProducts → Product.Name.
        /// </param>
        public static string BuildUserFilter(
            WbFilter filter,
            IReadOnlyList<WbProduct>? bagProducts = null,
            int maxLength = 4096)
        {
            if (filter == null)
                return "Фильтр не задан";

            if (maxLength < 256)
                maxLength = 256;

            var listLines = BuildFilterListLines(filter, bagProducts, out var listTotalCount, out var listLabel);
            var strategies = BuildFilterStrategies(filter);
            var footer = strategies + new string('─', 40);

            var sb = new StringBuilder();
            sb.AppendLine("*ИНФОРМАЦИЯ О ФИЛЬТРЕ*");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine("*Базовые параметры:*");
            sb.AppendLine($"   💰 Мин. скидка: {filter.DiscontMinPercent}%");
            sb.AppendLine($"   💬 Мин. отзывов: {filter.MinReviewsCount}");
            sb.AppendLine($"   ⭐ Мин. рейтинг: {filter.MinRating}");
            sb.AppendLine();
            sb.AppendLine("🔹 *Фильтрация товаров:*");
            sb.AppendLine($"   {GetFilterTypeLine(filter.ProductsFilterType)}");

            if (listLabel != null)
            {
                sb.AppendLine($"   📦 {listLabel}: *{listTotalCount}*");
                if (listTotalCount == 0)
                {
                    sb.AppendLine(listLabel == "Категорий"
                        ? "   (список пуст)"
                        : "   (корзина пуста)");
                }
                else
                {
                    AppendBoundedLines(sb, listLines, footer.Length, maxLength);
                }
            }

            sb.AppendLine();

            // Если хвост не влезает — обрезаем список ещё сильнее (повторная сборка не нужна:
            // AppendBoundedLines уже резервировал footer.Length).
            if (sb.Length + footer.Length > maxLength)
            {
                var keep = Math.Max(0, maxLength - footer.Length - 3);
                var head = sb.ToString(0, Math.Min(sb.Length, keep)).TrimEnd() + "...\n\n";
                return head + footer;
            }

            sb.Append(footer);

            if (sb.Length > maxLength)
            {
                return sb.ToString(0, maxLength - 3) + "...";
            }

            return sb.ToString();
        }

        private static string GetFilterTypeLine(ProductsFilterType type) => type switch
        {
            ProductsFilterType.None => "🔘 Тип: *Не применяется*",
            ProductsFilterType.OwnBag => "🛍️ Тип: *Собственная корзина*",
            ProductsFilterType.Categories_BlackList => "🚫 Тип: *Черный список категорий*",
            ProductsFilterType.Categories_WhiteList => "✅ Тип: *Белый список категорий*",
            _ => $"Тип: *{type}*"
        };

        private static string BuildFilterStrategies(WbFilter filter)
        {
            var sb = new StringBuilder();
            sb.AppendLine("🔹 *Стратегии расчета цены:*");

            if (filter.ReferencePriceStrartegies != null && filter.ReferencePriceStrartegies.Any())
            {
                sb.AppendLine($"   📊 Выбрано стратегий: *{filter.ReferencePriceStrartegies.Count}*");

                foreach (var strategy in filter.ReferencePriceStrartegies)
                {
                    string emoji = strategy switch
                    {
                        ReferencePriceStrategy.LastKnownPrice => "⏱️",
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
                sb.AppendLine("   📊 Стратегии: *все*");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private static List<string> BuildFilterListLines(
            WbFilter filter,
            IReadOnlyList<WbProduct>? bagProducts,
            out int totalCount,
            out string? listLabel)
        {
            listLabel = null;
            totalCount = 0;
            var lines = new List<string>();

            switch (filter.ProductsFilterType)
            {
                case ProductsFilterType.OwnBag:
                    listLabel = "Товаров";
                    if (bagProducts != null)
                    {
                        totalCount = bagProducts.Count;
                        foreach (var p in bagProducts)
                        {
                            var name = string.IsNullOrWhiteSpace(p.Name) ? "Без названия" : p.Name.Trim();
                            lines.Add($"   • {name}");
                        }
                    }
                    else
                    {
                        var bags = filter.BagProducts ?? [];
                        totalCount = bags.Count;
                        foreach (var b in bags)
                        {
                            var name = b.Product?.Name;
                            lines.Add(string.IsNullOrWhiteSpace(name)
                                ? "   • Без названия"
                                : $"   • {name.Trim()}");
                        }
                    }

                    break;

                case ProductsFilterType.Categories_BlackList:
                case ProductsFilterType.Categories_WhiteList:
                    listLabel = "Категорий";
                    var cats = filter.FilterCategories ?? [];
                    totalCount = cats.Count;
                    foreach (var c in cats)
                    {
                        lines.Add(
                            c.Category != null && !string.IsNullOrEmpty(c.Category.Name)
                                ? $"   • {c.Category.Name} (ID: {c.CategoryId})"
                                : $"   • ID категории: {c.CategoryId}");
                    }

                    break;
            }

            return lines;
        }

        /// <summary>
        /// Добавляет строки списка, оставляя reservedTail символов под блок стратегий.
        /// </summary>
        private static void AppendBoundedLines(
            StringBuilder sb,
            List<string> lines,
            int reservedTail,
            int maxLength)
        {
            var added = 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var remainingAfterThis = lines.Count - (added + 1);
                var moreSuffix = remainingAfterThis > 0
                    ? $"\n   ... и ещё {remainingAfterThis}"
                    : string.Empty;

                var chunk = line + "\n";
                if (sb.Length + chunk.Length + moreSuffix.Length + reservedTail <= maxLength)
                {
                    sb.Append(chunk);
                    added++;
                }
                else
                {
                    var left = lines.Count - added;
                    if (left > 0)
                    {
                        var suffix = $"   ... и ещё {left}\n";
                        if (sb.Length + suffix.Length + reservedTail <= maxLength)
                        {
                            sb.Append(suffix);
                        }
                    }

                    break;
                }
            }
        }

        private static string ToReadableString(ReferencePriceStrategy strategy)
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
                var line = $"• {product.Name ?? "Без названия"}";

                int remaining = totalProducts - (addedCount + 1);
                string suffix = remaining > 0 ? $"\n\n... и ещё {remaining} {GetProductPlural(remaining)}" : "";
                int newlineLength = (addedCount == 0) ? 0 : 1;

                if (sb.Length + newlineLength + line.Length + suffix.Length <= maxLength)
                {
                    if (addedCount > 0)
                        sb.AppendLine();

                    sb.Append(line);
                    addedCount++;
                }
                else
                {
                    if (remaining > 0 && sb.Length + suffix.Length <= maxLength)
                    {
                        sb.Append(suffix);
                    }
                    break;
                }
            }

            if (sb.Length > maxLength)
            {
                return sb.ToString(0, Math.Max(0, maxLength - 3)) + "...";
            }

            return sb.ToString();
        }

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
