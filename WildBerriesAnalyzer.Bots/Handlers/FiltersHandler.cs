using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class FiltersHandler : IMessageHandler
    {
        private readonly IFiltersRepository _filtersRepository;
        private readonly IProductsRepository _productsRepository;

        public BotUserPlace HandlePlace => BotUserPlace.Filters;

        public FiltersHandler(
            IFiltersRepository filtersRepository,
            IProductsRepository productsRepository)
        {
            _filtersRepository = filtersRepository;
            _productsRepository = productsRepository;
        }

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (!ExpectedUserMessages.FiltersExceptsPlaces.TryGetValue(message.Text, out var newPlace))
            {
                return ErrorMessageHelper.CreateMessage();
            }

            string? info = null;
            if (message.Text == ExpectedUserMessages.Filters_Info ||
                message.Text == ExpectedUserMessages.Filters_ChangeProducts)
            {
                var userFilter = await _filtersRepository.GetOrCreateByUserIdAsync(message.UserId.Value);

                if (message.Text == ExpectedUserMessages.Filters_Info)
                {
                    userFilter = await _filtersRepository.GetFilterWithDetailsAsync(message.UserId.Value)
                                 ?? userFilter;
                    var bagProducts = userFilter.ProductsFilterType == ProductsFilterType.OwnBag
                        ? await _productsRepository.GetUserBagProductsAsync(message.UserId.Value)
                        : null;
                    info = BotMessageBuilder.BuildUserFilter(userFilter, bagProducts);
                }
                else
                {
                    if (userFilter.ProductsFilterType == ProductsFilterType.None)
                    {
                        return ErrorMessageHelper.CreateMessage("Сначала необходимо выбрать тип фильтрации.");
                    }
                    else if (userFilter.ProductsFilterType == ProductsFilterType.OwnBag)
                    {
                        newPlace = BotUserPlace.Filters_ChangeProducts_OwnBag;
                    }
                    else
                    {
                        // Меню категорий пока не реализовано — не уводим в WhiteList/BlackList
                        // (там нет handler → бот зависал / падал на клавиатуре).
                        return ErrorMessageHelper.CreateMessage(
                            "Управление категориями пока недоступно.\n" +
                            "Выберите тип фильтрации «Корзина», затем снова откройте «Корзина/категории».");
                    }
                }
            }

            return new BotMessage
            {
                NewUserPlace = newPlace,
                Text = info ?? ExpectedBotAnswers.GetTextForPlace(newPlace!.Value)
            };
        }
    }
}
