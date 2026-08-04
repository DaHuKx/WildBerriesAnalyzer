using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Models.Messages
{
    public class UserMessage : MessageBase
    {
        public BotUserPlace UserPlace { get; set; }
    }
}
