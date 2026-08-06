using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    /// <summary>
    /// Добавление товаров в корзину бота по ссылке WB ?shareId=…
    /// </summary>
    public class AddProductsShareHandler : IMessageHandler
    {
        private const string UserFacingProblem = "Возникла проблема. Попробуйте позже.";

        private readonly IFiltersService _filtersService;
        private readonly BasketShareUrlValidator _basketShareUrlValidator;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_ChangeProducts_OwnBag_AddShare;

        public AddProductsShareHandler(
            IFiltersService filtersService,
            BasketShareUrlValidator basketShareUrlValidator)
        {
            _filtersService = filtersService;
            _basketShareUrlValidator = basketShareUrlValidator;
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

            var input = message.Text?.Trim() ?? string.Empty;
            var validation = _basketShareUrlValidator.Validate(input);
            if (!validation.IsValid)
            {
                return ErrorMessageHelper.CreateMessage(validation.Errors.First().ErrorMessage);
            }

            if (!message.UserId.HasValue)
            {
                return ErrorMessageHelper.CreateMessage(UserFacingProblem);
            }

            try
            {
                var result = await _filtersService.AddProductsToBagFromBasketShareAsync(
                    message.UserId.Value,
                    input);

                if (result.AddedProducts.Count == 0)
                {
                    return new BotMessage
                    {
                        Text =
                            $"Новых товаров нет — всё из ссылки уже в корзине.\n" +
                            $"Всего в корзине: {result.BagProducts.Count}."
                    };
                }

                return new BotMessage
                {
                    Text = BotMessageBuilder.BuildProductsMessage(
                        result.AddedProducts,
                        $"Добавлено новых: {result.AddedProducts.Count}\n" +
                        $"Всего в корзине: {result.BagProducts.Count}\n\n" +
                        "Новые товары:")
                };
            }
            catch (ArgumentException ex)
            {
                return ErrorMessageHelper.CreateMessage(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-сообщения (пуста/устарела) — как есть; остальное — без деталей.
                if (ex.Message.Contains("корзин", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("ссылк", StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorMessageHelper.CreateMessage(ex.Message);
                }

                return ErrorMessageHelper.CreateMessage(UserFacingProblem);
            }
            catch
            {
                return ErrorMessageHelper.CreateMessage(UserFacingProblem);
            }
        }
    }
}
