using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using VkNet.Abstractions;
using VkNet.Enums.Filters;
using VkNet.Enums.StringEnums;
using VkNet.Exception;
using VkNet.Model;
using WildBerriesAnalyzer.Bots.Clients.Helpers;
using WildBerriesAnalyzer.Bots.Clients.Interfaces;
using WildBerriesAnalyzer.Bots.Enums;
using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Consts;

namespace WildBerriesAnalyzer.Bots.Clients
{
    public class VkClient : IClient
    {
        private ulong _groupId;

        private readonly IVkApi _api;
        private readonly Random _random;
        private readonly ILogger<VkClient> _logger;

        private ulong? _ts;

        public event BotMessageReceivedHandler? OnMessageReceived;

        public BotType BotType => BotType.Vk;

        public VkClient(IVkApi api, ILogger<VkClient> logger)
        {
            _api = api;
            _logger = logger;
            _random = new Random();
            _groupId = ResolveGroupId();
        }

        public void Initialize()
        {
            _api.Authorize(new ApiAuthParams
            {
                AccessToken = ResolveAccessToken(),
                Settings = Settings.All
            });

            // Уведомление админу не должно валить процесс при сбое DNS/сети.
            try
            {
                SendMessage(new BotMessage
                {
                    BotType = BotType.Vk,
                    Text = "Запущен.",
                    UserSocialId = AdminAccounts.VkId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось отправить «Запущен.» админу (VK сеть). Бот продолжит работу.");
            }
        }

        public void SendMessage(BotMessage message)
        {
            if (!_api.IsAuthorized)
            {
                return;
            }

            try
            {
                _api.Messages.Send(new MessagesSendParams
                {
                    Message = message.Text,
                    UserId = long.Parse(message.UserSocialId),
                    RandomId = _random.Next()
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VK SendMessage failed user={UserId}", message.UserSocialId);
                throw;
            }
        }

        public async Task SendMessageAsync(BotMessage message)
        {
            if (!_api.IsAuthorized)
            {
                return;
            }

            var sendParams = new MessagesSendParams
            {
                Message = message.Text,
                UserId = long.Parse(message.UserSocialId),
                RandomId = _random.Next()
            };

            if (message.NewUserPlace is { } place)
            {
                sendParams.Keyboard = VkKeyboardBuilder.GetKeyboardByPlace(place);
            }

            try
            {
                await _api.Messages.SendAsync(sendParams);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VK SendMessageAsync failed user={UserId}", message.UserSocialId);
                throw;
            }
        }

        public async Task StartListeningMessages()
        {
            while (true)
            {
                try
                {
                    var response = await GetBotsLongPollHistoryResponseAsync();

                    if (response?.Updates is null || response.Updates.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    foreach (var update in response.Updates.Where(u => u.Type.Value == GroupUpdateType.MessageNew))
                    {
                        CheckUpdate(update);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VK long poll cycle error");
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private void CheckUpdate(GroupUpdate update)
        {
            if (update.Type.Value != GroupUpdateType.MessageNew) return;

            var message = update.Instance as MessageNew;

            if (message is null) return;

            OnMessageReceived?.Invoke(new UserMessage
            {
                BotType = BotType.Vk,
                Text = message.Message.Text,
                UserSocialId = message.Message.FromId!.Value.ToString()
            });
        }

        private async Task<BotsLongPollHistoryResponse?> GetBotsLongPollHistoryResponseAsync()
        {
            var pollResponse = await _api.Groups.GetLongPollServerAsync(_groupId);

            try
            {
                var response = await _api.Groups.GetBotsLongPollHistoryAsync(new BotsLongPollHistoryParams
                {
                    Server = pollResponse.Server,
                    Ts = _ts ?? pollResponse.Ts,
                    Key = pollResponse.Key,
                    Wait = 90
                });

                _ts = response.Ts;

                return response;
            }
            catch (LongPollOutdateException outDateEx)
            {
                _ts = outDateEx.Ts;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetBotsLongPollHistory failed");
                return null;
            }
        }

        private async Task<string> UploadFile(string serverUrl, string file, string fileExtension)
        {
            var data = File.ReadAllBytes(file);

            using var client = new HttpClient();
            var requestContent = new MultipartFormDataContent();
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
            requestContent.Add(content, "file", $"file.{fileExtension}");

            var response = await client.PostAsync(serverUrl, requestContent);
            return Encoding.Default.GetString(await response.Content.ReadAsByteArrayAsync());
        }

        private static string ResolveAccessToken()
        {
            var fromEnv = Environment.GetEnvironmentVariable("VK_BOT_ACCESS_TOKEN")
                ?? Environment.GetEnvironmentVariable("VkBot__AccessToken");
            return string.IsNullOrWhiteSpace(fromEnv)
                ? Properties.Resources.VkKey
                : fromEnv.Trim();
        }

        private static ulong ResolveGroupId()
        {
            var raw = Environment.GetEnvironmentVariable("VK_BOT_GROUP_ID")
                ?? Environment.GetEnvironmentVariable("VkBot__GroupId");
            if (ulong.TryParse(raw, out var groupId) && groupId > 0)
            {
                return groupId;
            }

            return 219811363;
        }
    }
}
