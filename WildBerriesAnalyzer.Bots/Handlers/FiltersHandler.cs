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

        public BotUserPlace HandlePlace => BotUserPlace.Filters;

        public FiltersHandler(IFiltersRepository filtersRepository)
        {
            _filtersRepository = filtersRepository;
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
                    info = BotMessageBuilder.BuildUserFilter(userFilter);
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
                    else if (userFilter.ProductsFilterType == ProductsFilterType.Categories_BlackList)
                    {
                        newPlace = BotUserPlace.Filters_ChangeProducts_BlackList;
                    }
                    else
                    {
                        newPlace = BotUserPlace.Filters_ChangeProducts_WhiteList;
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
