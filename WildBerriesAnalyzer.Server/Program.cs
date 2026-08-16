using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;
using Serilog;
using WildBerriesAnalyzer.Business;
using WildBerriesAnalyzer.Business.Helpers;
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
using WildBerriesAnalyzer.Data.Services;
using WildBerriesAnalyzer.Domain.Interfaces;
using WildBerriesAnalyzer.Server.Middleware;
using WildBerriesAnalyzer.Server.Options;
using WildBerriesAnalyzer.Server.Services;
using WildBerriesAnalyzer.Server.Services.Auth;
using WildBerriesAnalyzer.Server.Services.PriceUpdate;
using WildBerriesAnalyzer.Server.Services.VkBot;
using WildBerriesAnalyzer.Server.Services.VkId;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

static string FirstNonEmpty(string? preferred, string fallback) =>
    string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Локально — 5146 на всех интерфейсах; в Docker — ASPNETCORE_URLS / ASPNETCORE_HTTP_PORTS.
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")))
    {
        builder.WebHost.UseUrls("http://0.0.0.0:5146");
    }

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: Path.Combine("logs", "server-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<WbScrapingAuthOptions>(
        builder.Configuration.GetSection(WbScrapingAuthOptions.SectionName));
    builder.Services.Configure<OzonScrapingAuthOptions>(
        builder.Configuration.GetSection(OzonScrapingAuthOptions.SectionName));
    builder.Services.Configure<PriceUpdateOptions>(
        builder.Configuration.GetSection(PriceUpdateOptions.SectionName));
    builder.Services.Configure<VkIdOptions>(
        builder.Configuration.GetSection(VkIdOptions.SectionName));
    builder.Services.Configure<VkBotOptions>(
        builder.Configuration.GetSection(VkBotOptions.SectionName));
    builder.Services.Configure<MobileVersionOptions>(
        builder.Configuration.GetSection(MobileVersionOptions.SectionName));

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Секция Jwt не найдена в конфигурации.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "WildBerriesAnalyzer API",
            Version = "v1"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header. Пример: Bearer {token}"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    builder.Services.AddScoped<WbDataBase>();

    builder.Services.AddScoped<IUsersRepository, UsersRepository>();
    builder.Services.AddScoped<IModersRepository, ModersRepository>();
    builder.Services.AddScoped<CategoryModerationService>();
    builder.Services.AddScoped<IClientVersionTracker, ClientVersionTracker>();
    builder.Services.AddScoped<IFiltersRepository, FiltersRepository>();
    builder.Services.AddScoped<IProductsRepository, ProductsRepository>();
    builder.Services.AddScoped<IPricesRepository, PricesRepository>();
    builder.Services.AddScoped<IPriceUpdateJobsRepository, PriceUpdateJobsRepository>();
    builder.Services.AddScoped<IActualDiscontsRepository, ActualDiscontsRepository>();
    builder.Services.AddScoped<IVkLinkCodesRepository, VkLinkCodesRepository>();
    builder.Services.AddScoped<IDiscontNotificationsRepository, DiscontNotificationsRepository>();
    builder.Services.AddSingleton<INotifier, ConsoleNotifier>();

    builder.Services.AddSingleton<IWbScrapingAuthStore>(provider =>
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
    builder.Services.AddSingleton<IWbScrapingAuthUpdater, WbScrapingAuthUpdater>();
    builder.Services.AddSingleton<IPriceUpdateScheduler, PriceUpdateScheduler>();
    builder.Services.AddScoped<IWildBerriesService, WildBerriesService>();
    builder.Services.AddSingleton(provider =>
    {
        var configured = provider.GetRequiredService<IOptions<OzonScrapingAuthOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("OzonScrapingAuth");
        var persistPath = OzonScrapingAuthLoader.ResolvePersistPath(
            configured.PersistFilePath,
            builder.Environment.ContentRootPath);
        var fromFile = OzonScrapingAuthLoader.LoadOrDefault(persistPath);
        var options = OzonScrapingAuthLoader.Merge(configured, fromFile);
        options.PersistFilePath = persistPath;

        logger.LogInformation(
            "Ozon scraping auth: file={AuthPath}, cookie={HasCookie}, useBrowser={UseBrowser}, concurrency={Concurrency}",
            persistPath,
            options.HasCookie ? "yes" : "no",
            options.UseBrowser,
            options.ProductConcurrency);

        return options;
    });
    builder.Services.AddSingleton<IOzonScrapingAuthUpdater>(provider =>
    {
        var options = provider.GetRequiredService<OzonScrapingAuthOptions>();
        return new OzonScrapingAuthUpdater(options, options.PersistFilePath);
    });
    builder.Services.AddSingleton<IOzonService>(provider =>
        new OzonService(provider.GetRequiredService<OzonScrapingAuthOptions>()));
    builder.Services.AddScoped<IDiscontsService, DiscontsService>();
    builder.Services.AddScoped<IActualDiscontsService, ActualDiscontsService>();
    builder.Services.AddScoped<IFiltersService, FiltersService>();
    builder.Services.AddScoped<IProductsService, ProductsService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddHttpClient<IVkIdOAuthClient, VkIdOAuthClient>()
        .ConfigurePrimaryHttpMessageHandler(() => Ipv4Http.CreateHandler(useCookies: false));
    builder.Services.AddHttpClient<IVkCommunityMessenger, VkCommunityMessenger>()
        .ConfigurePrimaryHttpMessageHandler(() => Ipv4Http.CreateHandler(useCookies: false));
    builder.Services.AddSingleton<IPendingRegistrationStore, PendingRegistrationStore>();

    builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
    builder.Services.AddSingleton<ITokenIssuer, TokenIssuer>();

    builder.Services.AddScoped<RegisterCredentialsValidator>();
    builder.Services.AddSingleton<LoginCredentialsValidator>();
    builder.Services.AddSingleton<RefreshCredentialsValidator>();
    builder.Services.AddSingleton<ProductIdValidator>();
    builder.Services.AddSingleton<BasketShareUrlValidator>();
    builder.Services.AddSingleton<OzonCartShareUrlValidator>();
    builder.Services.AddSingleton<ProductNameValidator>();
    builder.Services.AddSingleton<WbFilterValidator>();

    builder.Services.AddHostedService<OzonBrowserWarmUpHostedService>();
    builder.Services.AddHostedService<PriceUpdateBackgroundService>();

    var app = builder.Build();

    var applyMigrations = string.Equals(
        Environment.GetEnvironmentVariable("APPLY_DB_MIGRATIONS"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    var migrateOnly = string.Equals(
        Environment.GetEnvironmentVariable("RUN_AND_EXIT_AFTER_MIGRATE"),
        "true",
        StringComparison.OrdinalIgnoreCase);

    if (applyMigrations || migrateOnly)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WbDataBase>();
        db.Database.Migrate();
        Log.Information("EF migrations applied.");
    }

    if (migrateOnly)
    {
        Log.Information("Migration-only mode: exiting.");
        return;
    }

#if DEBUG
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "WildBerriesAnalyzer API v1");
            options.RoutePrefix = "swagger";
        });
    }
#endif

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath}{QueryString} responded {StatusCode} in {Elapsed:0.0000} ms from {RemoteIp}";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("QueryString", httpContext.Request.QueryString.HasValue
                ? httpContext.Request.QueryString.Value
                : string.Empty);
            diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "-");
        };
    });

    // В Development Mobile ходит по HTTP (LAN IP). UseHttpsRedirection иначе
    // отдаёт 307 на https://localhost:..., и телефон зависает до Timeout (60 с).
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseMiddleware<AuthMiddleware>();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Сервер завершился из‑за необработанного исключения");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
