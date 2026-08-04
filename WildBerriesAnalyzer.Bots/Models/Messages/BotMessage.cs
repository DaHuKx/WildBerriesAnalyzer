using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Models.Messages
{
    public class BotMessage : MessageBase
    {
        public BotUserPlace? NewUserPlace { get; set; }
    }
}
