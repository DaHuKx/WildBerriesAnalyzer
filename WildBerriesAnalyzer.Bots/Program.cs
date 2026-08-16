using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VkNet;
using VkNet.Abstractions;
using VkNet.Abstractions.Utils;
using WildBerriesAnalyzer.Bots.Clients;
using WildBerriesAnalyzer.Bots.Clients.Interfaces;
using WildBerriesAnalyzer.Bots.Handlers;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Services;
using WildBerriesAnalyzer.Business.Options;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.OzonScraping;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.WbScraping;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   // Сбой одного сообщения (DNS VK и т.п.) не должен гасить весь процесс.
                   services.Configure<HostOptions>(options =>
                   {
                       options.BackgroundServiceExceptionBehavior =
                           BackgroundServiceExceptionBehavior.Ignore;
                   });

                   // Singleton: BotsManager (hosted) получает зависимости в ctor.
                   services.AddDbContext<WbDataBase>(ServiceLifetime.Singleton);

                   services.Configure<WbScrapingAuthOptions>(
                       hostContext.Configuration.GetSection(WbScrapingAuthOptions.SectionName));

                   services.AddSingleton<IWbScrapingAuthStore>(provider =>
                   {
                       var configured = provider.GetRequiredService<IOptions<WbScrapingAuthOptions>>().Value;
                       var defaults = WildBerriesService.CreateDefaultOptions();
                       var options = new WbScrapingAuthOptions
                       {
                           AccessToken = FirstNonEmpty(configured.AccessToken, defaults.AccessToken),
                           Cookie = FirstNonEmpty(configured.Cookie, defaults.Cookie),
                           DeviceId = FirstNonEmpty(configured.DeviceId, defaults.DeviceId),
                           UserAgent = FirstNonEmpty(configured.UserAgent, defaults.UserAgent),
                           SpaVersion = FirstNonEmpty(configured.SpaVersion, defaults.SpaVersion),
                           SecChUa = FirstNonEmpty(configured.SecChUa, defaults.SecChUa),
                           PersistFilePath = FirstNonEmpty(configured.PersistFilePath, defaults.PersistFilePath)
                       };
                       return new FileWbScrapingAuthStore(options);
                   });
                   services.AddSingleton<IWbScrapingAuthUpdater, WbScrapingAuthUpdater>();

                   services.Configure<OzonScrapingAuthOptions>(
                       hostContext.Configuration.GetSection(OzonScrapingAuthOptions.SectionName));
                   services.AddSingleton(provider =>
                   {
                       var configured = provider.GetRequiredService<IOptions<OzonScrapingAuthOptions>>().Value;
                       var persistPath = OzonScrapingAuthLoader.ResolvePersistPath(
                           configured.PersistFilePath,
                           hostContext.HostingEnvironment.ContentRootPath);
                       var fromFile = OzonScrapingAuthLoader.LoadOrDefault(persistPath);
                       var options = OzonScrapingAuthLoader.Merge(configured, fromFile);
                       options.PersistFilePath = persistPath;
                       return options;
                   });
                   services.AddSingleton<IOzonScrapingAuthUpdater>(provider =>
                   {
                       var options = provider.GetRequiredService<OzonScrapingAuthOptions>();
                       return new OzonScrapingAuthUpdater(options, options.PersistFilePath);
                   });
                   services.AddSingleton<AdminWbAuthCommandService>();

                   services.AddSingleton<IWildBerriesService, WildBerriesService>();
                   services.AddSingleton<IOzonService>(provider =>
                       new OzonService(provider.GetRequiredService<OzonScrapingAuthOptions>()));
                   services.AddSingleton<IDiscontsService, DiscontsService>();
                   services.AddSingleton<IActualDiscontsService, ActualDiscontsService>();
                   services.AddSingleton<ProductIdValidator>();
                   services.AddSingleton<BasketShareUrlValidator>();
                   services.AddSingleton<OzonCartShareUrlValidator>();
                   services.AddSingleton<WbFilterValidator>();
                   services.AddSingleton<IFiltersService, FiltersService>();

                   services.AddSingleton<IProductsRepository, ProductsRepository>();
                   services.AddSingleton<IUsersRepository, UsersRepository>();
                   services.AddSingleton<IFiltersRepository, FiltersRepository>();
                   services.AddSingleton<IPriceUpdateJobsRepository, PriceUpdateJobsRepository>();
                   services.AddSingleton<IActualDiscontsRepository, ActualDiscontsRepository>();
                   services.AddSingleton<IVkLinkCodesRepository, VkLinkCodesRepository>();
                   services.AddSingleton<IDiscontNotificationsRepository, DiscontNotificationsRepository>();
                   services.AddSingleton<IAccountService, AccountService>();

                   // Один VK-клиент на процесс: BotsManager инициализирует, CheckPriceService шлёт алерты.
                   services.AddSingleton<IRestClient, VkIpv4RestClient>();
                   services.AddSingleton<IVkApi>(sp =>
                   {
                       var api = new VkApi(sp.GetService<Microsoft.Extensions.Logging.ILogger<VkApi>>());
                       api.RestClient = sp.GetRequiredService<IRestClient>();
                       return api;
                   });
                   services.AddSingleton<IClient, VkClient>();

                   services.AddHostedService<BotsManager>();
                   services.AddHostedService<CheckPriceService>();

                   services.AddSingleton<IMessageHandler, StartHandler>();
                   services.AddSingleton<IMessageHandler, MenuHandler>();
                   services.AddSingleton<IMessageHandler, FiltersHandler>();
                   services.AddSingleton<IMessageHandler, FiltersPercentHandler>();
                   services.AddSingleton<IMessageHandler, FiltersRatingHandler>();
                   services.AddSingleton<IMessageHandler, FiltersReviewsHandler>();
                   services.AddSingleton<IMessageHandler, FiltersStrategiesHandler>();
                   services.AddSingleton<IMessageHandler, FiltersTypeHandler>();
                   services.AddSingleton<IMessageHandler, FiltersChangeOwnBagHandler>();
                   services.AddSingleton<IMessageHandler, FiltersAddOwnBagHandler>();
                   services.AddSingleton<IMessageHandler, AddProductsShareHandler>();
                   services.AddSingleton<IMessageHandler, AddProductsHandler>();
                   services.AddSingleton<IMessageHandler, AddProductsNameHandler>();
                   services.AddSingleton<IMessageHandler, AddProductsIdHandler>();
               })
               .Build();

await host.RunAsync();

static string FirstNonEmpty(string? preferred, string fallback) =>
    string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;