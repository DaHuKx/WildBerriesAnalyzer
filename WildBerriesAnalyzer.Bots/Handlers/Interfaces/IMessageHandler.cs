using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers.Interfaces
{
    public interface IMessageHandler
    {
        BotUserPlace HandlePlace { get; }

        Task<BotMessage> HandleMessage(UserMessage message);
    }
}
