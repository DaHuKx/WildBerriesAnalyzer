using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class AddProductsHandler : IMessageHandler
    {
        public BotUserPlace HandlePlace => BotUserPlace.AddProducts;

        public Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (!ExpectedUserMessages.AddProductsExpectsPlaces.TryGetValue(message.Text, out var newPlace))
            {
                return Task.FromResult(ErrorMessageHelper.CreateMessage());
            }

            return Task.FromResult(new BotMessage
            {
                NewUserPlace = newPlace,
                Text = ExpectedBotAnswers.GetTextForPlace(newPlace!.Value)
            });
        }
    }
}
