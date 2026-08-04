using WildBerriesAnalyzer.Bots.Enums;

namespace WildBerriesAnalyzer.Bots.Models.Messages
{
    public class MessageBase
    {
        public int? UserId { get; set; }
        public string UserSocialId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public BotType BotType { get; set; }
    }
}
