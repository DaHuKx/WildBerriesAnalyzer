using FluentValidation.Results;
using System.Text;
using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class AddProductsIdHandler : IMessageHandler
    {
        private readonly IWildBerriesService _wildBerriesService;
        private readonly IProductsRepository _productsRepository;

        private readonly ProductIdValidator _validator;

        public BotUserPlace HandlePlace => BotUserPlace.AddProducts_Ids;

        public AddProductsIdHandler(IWildBerriesService wildBerriesService,
                                    IProductsRepository productsRepository)
        {
            _wildBerriesService = wildBerriesService;
            _productsRepository = productsRepository;

            _validator = new ProductIdValidator();
        }

        public async Task<BotMessage> HandleMessage(UserMessage message)
        {
            if (ExpectedUserMessages.IsBackMessage(message.Text))
            {
                return new BotMessage
                {
                    NewUserPlace = BotUserPlace.AddProducts,
                    Text = ExpectedBotAnswers.AddProducts
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

            try
            {
                var products = await _wildBerriesService.GetProductsForIdsAsync(validIds);
                var result = await _productsRepository.AddRangeAsync(products);

                return new BotMessage
                {
                    Text = BotMessageBuilder.BuildProductsMessage(result.ToList(), "Добавленные продукты:"),
                    NewUserPlace = null
                };
            }
            catch
            {
                return new BotMessage
                {
                    Text = $"Возникла ошибка во время добавления товаров, попробуйте позже.",
                    NewUserPlace = null
                };
            }
        }
    }
}
