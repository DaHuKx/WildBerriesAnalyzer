using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    /// <summary>
    /// Добавление товаров в корзину бота по ссылке WB ?shareId=…
    /// </summary>
    public class AddProductsShareHandler : IMessageHandler
    {
        private readonly IFiltersService _filtersService;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_ChangeProducts_OwnBag_AddShare;

        public AddProductsShareHandler(IFiltersService filtersService)
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

            var input = message.Text?.Trim() ?? string.Empty;
            if (!ProductHelper.TryExtractBasketShareId(input, out _) &&
                (input.Contains('/') || input.Contains('?') || input.Length < 4))
            {
                return ErrorMessageHelper.CreateMessage(
                    "Некорректная ссылка. Ожидается вида:\n" +
                    "https://www.wildberries.ru/basket?shareId=…\n\n" +
                    "Скопируйте ссылку «Поделиться корзиной» в Wildberries и отправьте снова.");
            }

            if (!message.UserId.HasValue)
            {
                return ErrorMessageHelper.CreateMessage("Не удалось определить пользователя. Напишите /start.");
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
                return ErrorMessageHelper.CreateMessage(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorMessageHelper.CreateMessage(
                    "Не удалось получить корзину Wildberries (нужна авторизация на сервере). Попробуйте позже.");
            }
            catch (HttpRequestException)
            {
                return ErrorMessageHelper.CreateMessage(
                    "Не удалось подключиться к Wildberries. Попробуйте позже.");
            }
            catch
            {
                return ErrorMessageHelper.CreateMessage(
                    "Возникла ошибка при добавлении корзины. Попробуйте позже.");
            }
        }
    }
}
