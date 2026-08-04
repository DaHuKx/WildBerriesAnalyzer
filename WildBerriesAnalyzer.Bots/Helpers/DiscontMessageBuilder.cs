using System.Globalization;
using System.Text;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;

namespace WildBerriesAnalyzer.Bots.Helpers
{
    public static class DiscontMessageBuilder
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static string Build(IReadOnlyList<Discont> disconts, string header)
        {
            if (disconts is null || disconts.Count == 0)
            {
                return $"{header}\n\nПодходящих скидок пока нет.";
            }

            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine();

            foreach (var d in disconts)
            {
                var name = d.Product?.Name ?? "Товар";
                var current = d.CurrentPrice?.Price ?? 0;
                var link = d.Product?.Link;

                sb.AppendLine($"• {name}");
                sb.AppendLine($"  −{Math.Round(d.DiscontPercent):0}%  |  {ToStrategyText(d.ReferencePriceStrategy)}");
                sb.AppendLine($"  Текущая: {FormatMoney(current)}  ({FormatDate(d.CurrentPrice?.CheckTime)})");

                if (d.ReferencePrice is { Price: > 0 } reference)
                {
                    var refDate = FormatReferencePeriod(
                        d.ReferencePricePeriodFrom,
                        AsNullable(reference.CheckTime));
                    sb.AppendLine($"  Референс: {FormatMoney(reference.Price)}  ({refDate})");
                }

                if (!string.IsNullOrWhiteSpace(link))
                {
                    sb.AppendLine($"  {link}");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static string ToStrategyText(ReferencePriceStrategy strategy) => strategy switch
        {
            ReferencePriceStrategy.LastKnownPrice => "Последняя цена",
            ReferencePriceStrategy.AveragePrice => "Средняя",
            ReferencePriceStrategy.Median => "Медиана",
            ReferencePriceStrategy.MinimumHistorical => "Ист. мин.",
            ReferencePriceStrategy.LowestPriceForLast30Days => "Мин. 30д",
            ReferencePriceStrategy.AveragePriceForLast30Days => "Средняя 30д",
            ReferencePriceStrategy.MedianPriceForLast30Days => "Медиана 30д",
            _ => strategy.ToString()
        };

        private static string FormatMoney(decimal value) =>
            $"{value.ToString("N0", Ru)} ₽";

        private static DateTime? AsNullable(DateTime? value) =>
            value is null || value.Value == default ? null : value;

        private static string FormatReferencePeriod(DateTime? from, DateTime? to)
        {
            if (from is null && to is null)
            {
                return "—";
            }

            if (from is null)
            {
                return FormatDate(to);
            }

            if (to is null)
            {
                return FormatDate(from);
            }

            var fromLocal = ToLocal(from.Value);
            var toLocal = ToLocal(to.Value);

            if (Math.Abs((toLocal - fromLocal).TotalMinutes) < 1)
            {
                return FormatDate(to);
            }

            return $"{fromLocal:dd.MM.yyyy} – {toLocal:dd.MM.yyyy}";
        }

        private static string FormatDate(DateTime? value)
        {
            if (value is null || value.Value == default)
            {
                return "—";
            }

            return ToLocal(value.Value).ToString("dd.MM.yyyy HH:mm", Ru);
        }

        private static DateTime ToLocal(DateTime value) =>
            value.Kind == DateTimeKind.Utc
                ? value.ToLocalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
    }
}
