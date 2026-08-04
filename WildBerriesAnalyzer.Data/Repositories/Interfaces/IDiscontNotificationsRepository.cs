using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IDiscontNotificationsRepository
    {
        /// <summary>
        /// Последние уведомления пользователя: ключ (ProductId, Strategy).
        /// </summary>
        Task<Dictionary<(int ProductId, ReferencePriceStrategy Strategy), DiscontNotification>> GetLastByUserAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task UpsertSentAsync(
            IEnumerable<DiscontNotification> notifications,
            CancellationToken cancellationToken = default);
    }
}
