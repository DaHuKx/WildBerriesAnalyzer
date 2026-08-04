using System.Text;
using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class FiltersStrategiesHandler : IMessageHandler
    {
        private readonly ReferencePriceStrategiesValidator _validator;

        private readonly IFiltersRepository _filtersRepository;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_Strategy;

        public FiltersStrategiesHandler(IFiltersRepository filtersRepository)
        {
            _filtersRepository = filtersRepository;

            _validator = new ReferencePriceStrategiesValidator();
        }

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

            var validationResult = _validator.Validate(message.Text);

            if (!validationResult.IsValid)
            {
                StringBuilder sb = new StringBuilder();

                foreach (var error in validationResult.Errors)
                {
                    sb.AppendLine(error.ErrorMessage);
                }

                return ErrorMessageHelper.CreateMessage($"Ошибка: \n{sb.ToString()}");
            }

            var userFilters = await _filtersRepository.GetOrCreateByUserIdAsync(message.UserId!.Value);

            var strategies = message.Text.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                                              .Select(s => (ReferencePriceStrategy)Enum.Parse(typeof(ReferencePriceStrategy), s))
                                              .ToList();

            userFilters.ReferencePriceStrartegies = strategies;

            await _filtersRepository.UpdateAsync(userFilters);

            return new BotMessage
            {
                NewUserPlace = BotUserPlace.Filters,
                Text = ExpectedBotAnswers.FiltersUpdateComplete
            };
        }
    }
}
