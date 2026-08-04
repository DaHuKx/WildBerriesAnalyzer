using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class MenuHandler : IMessageHandler
    {
        private const int MaxDiscountsInReply = 10;

        private readonly IActualDiscontsService _actualDiscontsService;

        public MenuHandler(IActualDiscontsService actualDiscontsService)
        {
            _actualDiscontsService = actualDiscontsService;
        }

        public BotUserPlace HandlePlace => BotUserPlace.Menu;

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (message.Text == ExpectedUserMessages.ActualDisconts)
            {
                if (message.UserId is null)
                {
                    return ErrorMessageHelper.CreateMessage("Не удалось определить пользователя.");
                }

                var disconts = await _actualDiscontsService.GetForUserAsync(
                    message.UserId.Value,
                    MaxDiscountsInReply);

                return new BotMessage
                {
                    NewUserPlace = BotUserPlace.Menu,
                    Text = DiscontMessageBuilder.Build(disconts, "Актуальные скидки по вашему фильтру:")
                };
            }

            if (!ExpectedUserMessages.MenuExpectsPlaces.TryGetValue(message.Text, out var newPlace)
                || newPlace is null)
            {
                return ErrorMessageHelper.CreateMessage();
            }

            return new BotMessage
            {
                NewUserPlace = newPlace,
                Text = ExpectedBotAnswers.GetTextForPlace(newPlace.Value)
            };
        }
    }
}
