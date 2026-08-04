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
    public class FiltersReviewsHandler : IMessageHandler
    {
        private readonly IFiltersRepository _filtersRepository;

        private readonly ReviewsCountValidator _reviewsCountValidator;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_Reviews;

        public FiltersReviewsHandler(IFiltersRepository filtersRepository)
        {
            _reviewsCountValidator = new ReviewsCountValidator();

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

            var validationResult = _reviewsCountValidator.Validate(message.Text);

            if (!validationResult.IsValid)
            {
                StringBuilder sb = new StringBuilder();

                foreach (var error in validationResult.Errors)
                {
                    sb.AppendLine(error.ErrorMessage);
                }

                return ErrorMessageHelper.CreateMessage($"Ошибка: \n{sb.ToString()}");
            }

            var userFilter = await _filtersRepository.GetOrCreateByUserIdAsync(message.UserId!.Value);

            userFilter.MinReviewsCount = int.Parse(message.Text.Replace(',', '.'));

            await _filtersRepository.UpdateAsync(userFilter);

            return new BotMessage
            {
                NewUserPlace = BotUserPlace.Filters,
                Text = ExpectedBotAnswers.FiltersUpdateComplete
            };
        }
    }
}
