using System;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Одноразовый код привязки VK-аккаунта к пользователю с логином.
    /// </summary>
    public class VkLinkCode : BaseDbEntity
    {
        public string Code { get; set; } = string.Empty;

        public int UserId { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        public WbUser? User { get; set; }
    }
}
