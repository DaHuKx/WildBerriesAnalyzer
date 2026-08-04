using System.Net.Http.Headers;
using System.Text;
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

        private ulong? _ts;

        public event BotMessageReceivedHandler? OnMessageReceived;

        public BotType BotType => BotType.Vk;

        public VkClient(IVkApi api)
        {
            _api = api;
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

            SendMessage(new BotMessage
            {
                BotType = BotType.Vk,
                Text = "Запущен.",
                UserSocialId = AdminAccounts.VkId
            });
        }

        public void SendMessage(BotMessage message)
        {
            if (!_api.IsAuthorized)
            {
                return;
            }

            _api.Messages.Send(new MessagesSendParams
            {
                Message = message.Text,
                UserId = long.Parse(message.UserSocialId),
                RandomId = _random.Next()
            });
        }

        public async Task SendMessageAsync(BotMessage message)
        {
            if (!_api.IsAuthorized)
            {
                return;
            }

            //List<MediaAttachment>? attachments = null;
            //if (!string.IsNullOrEmpty(message.DocumentPath))
            //{
            //    var server = await _api.Docs.GetMessagesUploadServerAsync(long.Parse(message.UserId), DocMessageType.Doc);

            //    var response = await UploadFile(server.UploadUrl, message.DocumentPath, Path.GetExtension(message.DocumentPath));

            //    string title = Path.GetFileName(message.DocumentPath);

            //    try
            //    {
            //        attachments = new List<MediaAttachment>
            //        {
            //            _api.Docs.Save(response, title ?? Guid.NewGuid().ToString(), null)
            //                     .First()
            //                     .Instance
            //        };
            //    }
            //    catch (Exception ex)
            //    {
            //        message.Message = "Ошибка во время отправки файла на сервер. Попробуй позже. 🚫";
            //        _logger.LogError($"UploadFile error: {ex.Message}, User: {message.UserId}");
            //    }
            //}

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

            await _api.Messages.SendAsync(sendParams);
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
                        Thread.Sleep(1000);
                        continue;
                    }

                    foreach (var update in response.Updates.Where(u => u.Type.Value == GroupUpdateType.MessageNew))
                    {
                        CheckUpdate(update);
                    }
                }
                catch (Exception ex)
                {
                    //logger
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

        private async Task<BotsLongPollHistoryResponse> GetBotsLongPollHistoryResponseAsync()
        {
            var pollResponse = await _api.Groups.GetLongPollServerAsync(_groupId);

            BotsLongPollHistoryResponse response;

            try
            {
                response = await _api.Groups.GetBotsLongPollHistoryAsync(new BotsLongPollHistoryParams
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
                var type = ex.GetType();

                return null;
            }
        }

        private async Task<string> UploadFile(string serverUrl, string file, string fileExtension)
        {
            // Получение массива байтов из файла
            var data = File.ReadAllBytes(file);

            // Создание запроса на загрузку файла на сервер
            using (var client = new HttpClient())
            {
                var requestContent = new MultipartFormDataContent();
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                requestContent.Add(content, "file", $"file.{fileExtension}");

                var response = client.PostAsync(serverUrl, requestContent).Result;
                return Encoding.Default.GetString(await response.Content.ReadAsByteArrayAsync());
            }
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
