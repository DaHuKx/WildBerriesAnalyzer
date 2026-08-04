using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class FiltersTypeHandler : IMessageHandler
    {
        private readonly IFiltersRepository _filtersRepository;

        public FiltersTypeHandler(IFiltersRepository filtersRepository)
        {
            _filtersRepository = filtersRepository;
        }

        public BotUserPlace HandlePlace => BotUserPlace.Filters_Type;

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (ExpectedUserMessages.IsBackMessage(message.Text))
            {
                return new BotMessage
                {
                    NewUserPlace = BotUserPlace.Filters,
                    Text = ExpectedBotAnswers.GetTextForPlace(BotUserPlace.Filters)
                };
            }

            ProductsFilterType? filterType = null;

            switch (message.Text)
            {
                case ExpectedUserMessages.Filters_Type_None:
                    filterType = ProductsFilterType.None;
                    break;
                case ExpectedUserMessages.Filters_Type_OwnBug:
                    filterType = ProductsFilterType.OwnBag;
                    break;
                case ExpectedUserMessages.Filters_Type_BlackList:
                    filterType = ProductsFilterType.Categories_BlackList;
                    break;
                case ExpectedUserMessages.Filters_Type_WhiteList:
                    filterType = ProductsFilterType.Categories_WhiteList;
                    break;
            }

            if (filterType is not null)
            {
                var userFilter = await _filtersRepository.GetOrCreateByUserIdAsync(message.UserId!.Value);

                userFilter.ProductsFilterType = filterType.Value;

                await _filtersRepository.UpdateAsync(userFilter);

                return new BotMessage
                {
                    Text = ExpectedBotAnswers.FiltersUpdateComplete,
                    NewUserPlace = BotUserPlace.Filters
                };
            }

            return ErrorMessageHelper.CreateMessage();
        }
    }
}
