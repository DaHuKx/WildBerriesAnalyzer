using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class StartHandler : IMessageHandler
    {
        public BotUserPlace HandlePlace => BotUserPlace.Start;

        public Task<BotMessage> HandleMessage(UserMessage message)
        {
            return Task.FromResult(new BotMessage()
            {
                Text = ExpectedBotAnswers.Menu,
                NewUserPlace = BotUserPlace.Menu
            });
        }
    }
}
