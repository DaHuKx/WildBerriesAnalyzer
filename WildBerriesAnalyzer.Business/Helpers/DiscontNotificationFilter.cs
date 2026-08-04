using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// Отбор скидок для автоматических алертов: без повторной рассылки «тех же» скидок.
    /// </summary>
    public static class DiscontNotificationFilter
    {
        /// <summary>
        /// Повторная отправка, если скидка стала глубже минимум на столько пунктов %.
        /// </summary>
        public const decimal MinPercentImprovement = 5m;

        /// <summary>
        /// Повторная отправка той же скидки не чаще этого интервала (если нет улучшения).
        /// </summary>
        public static readonly TimeSpan ResendTtl = TimeSpan.FromDays(7);

        public static List<Discont> FilterNewOrImproved(
            IEnumerable<Discont> candidates,
            IReadOnlyDictionary<(int ProductId, ReferencePriceStrategy Strategy), DiscontNotification> lastSent,
            DateTime utcNow,
            int? limit = null)
        {
            var filtered = candidates
                .Where(d => d.Product != null)
                .Where(d => ShouldNotify(d, lastSent, utcNow))
                .OrderByDescending(d => d.DiscontPercent)
                .AsEnumerable();

            if (limit is > 0)
            {
                filtered = filtered.Take(limit.Value);
            }

            return filtered.ToList();
        }

        public static bool ShouldNotify(
            Discont candidate,
            IReadOnlyDictionary<(int ProductId, ReferencePriceStrategy Strategy), DiscontNotification> lastSent,
            DateTime utcNow)
        {
            if (candidate.Product is null)
            {
                return false;
            }

            var key = (candidate.Product.Id, candidate.ReferencePriceStrategy);
            if (!lastSent.TryGetValue(key, out var last))
            {
                return true;
            }

            if (utcNow - last.SentAt >= ResendTtl)
            {
                return true;
            }

            var currentPrice = candidate.CurrentPrice?.Price ?? 0m;
            if (currentPrice > 0 && currentPrice < last.CurrentPrice)
            {
                return true;
            }

            if (candidate.DiscontPercent >= last.DiscontPercent + MinPercentImprovement)
            {
                return true;
            }

            return false;
        }
    }
}
