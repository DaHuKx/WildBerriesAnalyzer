using System.Text;
using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Helpers;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Handlers
{
    public class AddProductsNameHandler : IMessageHandler
    {
        private readonly IWildBerriesService _wildberriesService;
        private readonly IProductsRepository _productsRepository;

        private readonly ProductNameValidator _validator;

        public BotUserPlace HandlePlace => BotUserPlace.AddProducts_Name;

        public AddProductsNameHandler(IWildBerriesService wildberriesService,
                                      IProductsRepository productsRepository)
        {
            _wildberriesService = wildberriesService;
            _productsRepository = productsRepository;

            _validator = new ProductNameValidator();
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

            var validationResult = _validator.Validate(message.Text);

            if (!validationResult.IsValid)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Некорректное название:");

                for (int i = 0; i < validationResult.Errors.Count; i++)
                {
                    sb.AppendLine($"{i + 1}: {validationResult.Errors[i].ErrorMessage}");
                }

                return new BotMessage
                {
                    NewUserPlace = null,
                    Text = sb.ToString()
                };
            }

            try
            {
                var products = await _wildberriesService.ParseProductsAsync(message.Text);
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
