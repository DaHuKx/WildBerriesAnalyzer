using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WildBerriesAnalyzer.Bots.Clients.Interfaces;
using WildBerriesAnalyzer.Bots.Enums;
using WildBerriesAnalyzer.Bots.Handlers.Interfaces;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Bots.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Bots.Services
{
    public class BotsManager : BackgroundService, IBotsManager
    {
        private readonly Dictionary<BotType, IClient> _bots;
        private readonly Dictionary<BotUserPlace, IMessageHandler> _handlers;
        private readonly IUsersRepository _usersRepository;
        private readonly IAccountService _accountService;
        private readonly AdminWbAuthCommandService _adminCommands;
        private readonly ILogger<BotsManager> _logger;

        private readonly ChannelReader<UserMessage> _reader;
        private readonly ChannelWriter<UserMessage> _writer;

        public BotsManager(
            IEnumerable<IClient> bots,
            IEnumerable<IMessageHandler> handlers,
            IUsersRepository usersRepository,
            IAccountService accountService,
            AdminWbAuthCommandService adminCommands,
            ILogger<BotsManager> logger)
        {
            _bots = bots.ToDictionary(b => b.BotType, b => b);
            _handlers = handlers.ToDictionary(h => h.HandlePlace, h => h);
            _usersRepository = usersRepository;
            _accountService = accountService;
            _adminCommands = adminCommands;
            _logger = logger;

            var channel = Channel.CreateUnbounded<UserMessage>();
            _reader = channel.Reader;
            _writer = channel.Writer;
        }

        private void InitializeBots()
        {
            foreach (var bot in _bots)
            {
                bot.Value.Initialize();
                _ = bot.Value.StartListeningMessages();
                bot.Value.OnMessageReceived += HandleMessage;
            }
        }

        public async void HandleMessage(UserMessage message)
        {
            await _writer.WriteAsync(message);
        }

        public async Task StartReadingAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessMessageAsync(message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // DNS/VK/handler ошибки не должны гасить весь Host (StopHost).
                    _logger.LogError(ex,
                        "Ошибка обработки сообщения VK user={UserSocialId} place-handler. Повтор при следующем сообщении.",
                        message.UserSocialId);
                }
            }
        }

        private async Task ProcessMessageAsync(UserMessage message)
        {
            if (_adminCommands.TryHandle(message, out var adminReply))
            {
                await _bots[message.BotType].SendMessageAsync(new BotMessage
                {
                    BotType = message.BotType,
                    UserSocialId = message.UserSocialId,
                    Text = adminReply
                });
                return;
            }

            // Привязка аккаунта — до создания orphan-пользователя и роутинга по BotPlace.
            if (AccountService.TryParseLinkCommand(message.Text, out _))
            {
                var linkResult = await _accountService.ConfirmVkLinkAsync(
                    message.UserSocialId,
                    message.Text);

                await _bots[message.BotType].SendMessageAsync(new BotMessage
                {
                    BotType = message.BotType,
                    UserSocialId = message.UserSocialId,
                    Text = linkResult.Message
                });
                return;
            }

            var user = await _usersRepository.GetUserByVkIdAsync(message.UserSocialId);

            if (user is null)
            {
                user = new WbUser
                {
                    VkId = message.UserSocialId,
                    BotPlace = BotUserPlace.Start
                };

                await _usersRepository.AddAsync(user);
            }

            message.UserId = user.Id;

            if (!_handlers.TryGetValue(user.BotPlace, out var handler))
            {
                _logger.LogWarning(
                    "Нет handler для BotPlace={Place}, userId={UserId} — сброс в Menu",
                    user.BotPlace, user.Id);
                user.BotPlace = BotUserPlace.Menu;
                await _usersRepository.UpdateAsync(user);
                handler = _handlers[BotUserPlace.Menu];
            }

            var result = await handler.HandleMessage(message);

            if (result.NewUserPlace != null)
            {
                user.BotPlace = result.NewUserPlace.Value;
                await _usersRepository.UpdateAsync(user);
            }
            else
            {
                result.NewUserPlace = user.BotPlace;
            }

            result.BotType = message.BotType;
            result.UserSocialId = message.UserSocialId;

            await _bots[message.BotType].SendMessageAsync(result);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            InitializeBots();
            await StartReadingAsync(stoppingToken);
        }
    }
}
