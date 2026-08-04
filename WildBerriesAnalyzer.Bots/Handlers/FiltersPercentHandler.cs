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
    public class FiltersPercentHandler : IMessageHandler
    {
        private readonly PercentValidator _validator;
        private readonly IFiltersRepository _filtersRepository;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_Percent;

        public FiltersPercentHandler(IFiltersRepository filtersRepository)
        {
            _validator = new PercentValidator();

            _filtersRepository = filtersRepository;
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

            if (message.UserId is not null)
            {
                var userFilter = await _filtersRepository.GetOrCreateByUserIdAsync(message.UserId.Value);

                userFilter?.DiscontMinPercent = int.Parse(message.Text);

                await _filtersRepository.UpdateAsync(userFilter);
            }

            return new BotMessage()
            {
                NewUserPlace = BotUserPlace.Filters,
                Text = ExpectedBotAnswers.FiltersUpdateComplete
            };
        }
    }
}
