using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class FiltersChangeOwnBagHandler : IMessageHandler
    {
        private readonly IProductsRepository _productsRepository;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_ChangeProducts_OwnBag;

        public FiltersChangeOwnBagHandler(IProductsRepository productsRepository)
        {
            _productsRepository = productsRepository;
        }

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (!ExpectedUserMessages.FiltersOwnBagExpectsPlaces.TryGetValue(message.Text, out var newPlace))
            {
                return ErrorMessageHelper.CreateMessage();
            }

            if (message.Text == ExpectedUserMessages.Filters_OwnBag_ProductsList)
            {
                var userBagProducts = await _productsRepository.GetUserBagProductsAsync(message.UserId!.Value);

                return new BotMessage
                {
                    Text = BotMessageBuilder.BuildProductsMessage(userBagProducts, "Товары в вашей корзине:")
                };
            }
            else if (message.Text == ExpectedUserMessages.Filters_OwnBag_Instruction)
            {
                return new BotMessage
                {
                    Text = ExpectedBotAnswers.Filters_ChangeProducts_OwnBag_Instruction
                };
            }

            return new BotMessage
            {
                NewUserPlace = newPlace,
                Text = ExpectedBotAnswers.GetTextForPlace(newPlace.Value)
            };
        }
    }
}
