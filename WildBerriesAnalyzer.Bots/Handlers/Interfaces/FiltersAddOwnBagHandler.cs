using FluentValidation.Results;
using System.Text;
using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers.Interfaces
{
    public class FiltersAddOwnBagHandler : IMessageHandler
    {
        private readonly ProductIdValidator _validator;
        private readonly IProductsRepository _productsRepository;
        private readonly IFiltersRepository _filtersRepository;
        private readonly IWildBerriesService _wildBerriesService;

        public BotUserPlace HandlePlace => BotUserPlace.Filters_ChangeProducts_OwnBag_Add;

        public FiltersAddOwnBagHandler(IProductsRepository productsRepository,
                                       IFiltersRepository filtersRepository,
                                       IWildBerriesService wildBerriesService)
        {
            _validator = new ProductIdValidator();
            _productsRepository = productsRepository;
            _filtersRepository = filtersRepository;
            _wildBerriesService = wildBerriesService;
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

            var ids = message.Text.Split(' ', '\n', '\t');

            List<string> validIds = new List<string>();
            StringBuilder sb = new StringBuilder();

            ValidationResult validationResult;
            foreach (var id in ids)
            {
                var trimmedId = id?.Trim();
                validationResult = _validator.Validate(trimmedId);

                if (!validationResult.IsValid)
                {
                    sb.AppendLine($"{id}: {validationResult.Errors.First().ErrorMessage}");
                }
                else
                {
                    var cleanArticle = ProductHelper.ExtractCleanArticle(trimmedId);

                    if (!validIds.Contains(cleanArticle))
                        validIds.Add(cleanArticle);
                }
            }

            if (validIds.Count == 0)
            {
                return new BotMessage
                {
                    Text = string.Join($"Не удалось добавить товары:\n", sb.ToString())
                };
            }

            var products = await _wildBerriesService.GetProductsForIdsAsync(validIds);

            if (products.Count == 0)
            {
                return ErrorMessageHelper.CreateMessage("Не удалось получить товары с WildBerries. Попробуйте позже.");
            }

            var dbProducts = await _productsRepository.GetOrAddProducts(products);

            var addedProducts = await _filtersRepository.AddProductsToUserBag(message.UserId.Value, dbProducts);

            return new BotMessage
            {
                Text = BotMessageBuilder.BuildProductsMessage(addedProducts, "Добавленные в корзину продукты:")
            };
        }
    }
}
