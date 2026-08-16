namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Аккаунт с доступом к ModerMobile (UserId = WbUser.Id).
    /// </summary>
    public class WbModer : BaseDbEntity
    {
        public int UserId { get; set; }

        public WbUser? User { get; set; }
    }
}
