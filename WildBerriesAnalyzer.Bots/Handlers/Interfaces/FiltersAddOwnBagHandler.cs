using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers.Interfaces
{
    public class FiltersAddOwnBagHandler : IMessageHandler
    {
        private const string UserFacingProblem = "Возникла проблема. Попробуйте позже.";

        private readonly IFiltersService _filtersService;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_ChangeProducts_OwnBag_Add;

        public FiltersAddOwnBagHandler(IFiltersService filtersService)
        {
            _filtersService = filtersService;
        }

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (ExpectedUserMessages.IsBackMessage(message.Text))
            {
                return new BotMessage
                {
                    Text = ExpectedBotAnswers.GetTextForPlace(BotUserPlace.Filters_ChangeProducts_OwnBag),
                    NewUserPlace = BotUserPlace.Filters_ChangeProducts_OwnBag
                };
            }

            if (!message.UserId.HasValue)
            {
                return ErrorMessageHelper.CreateMessage(UserFacingProblem);
            }

            var ids = (message.Text ?? string.Empty)
                .Split([' ', '\n', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);

            if (ids.Length == 0)
            {
                return ErrorMessageHelper.CreateMessage("Укажите артикулы или ссылки на товары WB/Ozon.");
            }

            try
            {
                var result = await _filtersService.AddProductsToBagAsync(message.UserId.Value, ids);
                var added = result.AddedProducts;
                if (added.Count == 0)
                {
                    return new BotMessage
                    {
                        Text =
                            $"Новых товаров нет — всё уже в корзине.\n" +
                            $"Всего в корзине: {result.BagProducts.Count}."
                    };
                }

                return new BotMessage
                {
                    Text = BotMessageBuilder.BuildProductsMessage(added, "Добавленные в корзину продукты:")
                };
            }
            catch (ArgumentException ex)
            {
                return ErrorMessageHelper.CreateMessage(ex.Message);
            }
            catch
            {
                return ErrorMessageHelper.CreateMessage(UserFacingProblem);
            }
        }
    }
}
