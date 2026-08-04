using WildBerriesAnalyzer.Business.ExcelCreation;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.VK;
using WildBerriesAnalyzer.ConsoleTest;
using WildBerriesAnalyzer.Data;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Domain.Models;

ConsoleNotifier consoleNotifier = new ConsoleNotifier();
ProductsRepository productsRepository = new ProductsRepository(new WbDataBase(), consoleNotifier);
WildBerriesService wbService = new WildBerriesService(new ConsoleNotifier());
PricesRepository pricesRepository = new PricesRepository(new WbDataBase(), consoleNotifier);
DiscontsService discontsService = new DiscontsService(new ProductsRepository(new WbDataBase(), consoleNotifier));
VkPublisher publisher = new VkPublisher();
Exceler exceler = new Exceler();

Discont? prevDiscont = null;

bool authResult;
do
{
    Console.Write("Код: ");
    var code = Console.ReadLine();

    authResult = publisher.Authorize(code);
}
while (!authResult);

while (true)
{
    var prods = await productsRepository.GetAllAsync();

    var parsed = await wbService.ParseProductsPricesAsync(prods);
    if (parsed.ProductsWithRefreshedMeta.Count > 0)
    {
        await productsRepository.UpdateRangeAsync(parsed.ProductsWithRefreshedMeta);
    }

    await pricesRepository.AddRangeAsync(parsed.Prices);

    var disconts = discontsService.GetDiscontsFromProducts(await productsRepository.GetProductsWithPricesAsync());

    if (disconts is not null &&
        disconts.Any())
    {
        var bestDiscont = disconts.MaxBy(d => d.DiscontPercent);

        var path = exceler.CreateDiscontsFile(disconts);
        var message = CreateMessage(disconts, bestDiscont);

        await publisher.CreatePost(message, path);
    }

    Console.WriteLine("Complete!");

    await Task.Delay(TimeSpan.FromHours(1));
}

string CreateMessage(IEnumerable<Discont> disconts, Discont best)
{
    return $"✅ Скидки WB от {DateTime.UtcNow}\n" +
           $"⭐ Лучшая скидка: {Math.Round(best.DiscontPercent)}%\n" +
           $"❗ Зафиксировано скидок: {disconts.Count()}";
}

