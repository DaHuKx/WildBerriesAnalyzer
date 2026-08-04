using System.Text;
using VkNet.Model;
using WildBerriesAnalyzer.Business;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.VK;
using WildBerriesAnalyzer.Data;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.VkAddProductBot;

VkAddingBot bot = new VkAddingBot();
GigaChater giga = new GigaChater();
WildBerriesService wildBerriesService = new WildBerriesService(new ConsoleNotifier());
ProductsRepository productsRepository = new ProductsRepository(new WbDataBase(), new ConsoleNotifier());

bool authResult;
do
{
    authResult = bot.Authorize();
}
while (!authResult);

bot.OnGotMessage += NewMessageReceived;

await bot.SendMessageAsync("Запущено успешно", 322039043);

await bot.StartListeningMessages();

async void NewMessageReceived(Message message)
{
    try
    {
        var idStrs = message.Text.Split(' ', '\n').Select(str => str.Trim());

        StringBuilder sb = new StringBuilder();

        if (idStrs.Count() > 10)
        {
            await bot.SendMessageAsync("Максимум 10 товаров за сообщение ⛔", message.UserId!.Value);
            return;
        }

        List<WbProduct> products = new List<WbProduct>();

        foreach (var idStr in idStrs)
        {
            if (!long.TryParse(idStr, out long id))
            {
                sb.AppendLine($"{idStr} - не удалось валидировать ⛔");
                continue;
            }

            var result = await wildBerriesService.GetProductsForIdAsync([idStr]);

            if (result is null ||
               !result.Any() ||
                result.FirstOrDefault() is null)
            {
                sb.AppendLine($"{idStr} - товар не найден на WB ⛔");
                continue;
            }

            var product = result.First();

            products.Add(product);
        }

        if (products.Any())
        {
            var addedProducts = await productsRepository.AddRangeAsync(products);

            if (!addedProducts.Any())
            {
                sb.AppendLine($"Остальные продукты уже добавлены✅");
            }
            else
            {
                foreach (var product in addedProducts)
                {
                    sb.AppendLine($"{product.Name} - Добавлено успешно! ✅");
                }
            }
        }

        await bot.SendMessageAsync(sb.ToString(), message.FromId!.Value);
    }
    catch
    {
        await bot.SendMessageAsync("Произошла ошибка во время выполнения запроса.", message.FromId!.Value);
    }
}