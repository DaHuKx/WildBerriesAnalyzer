using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.StringEnums;
using VkNet.Exception;
using VkNet.Model;
using WildBerriesAnalyzer.Business.Properties;

namespace WildBerriesAnalyzer.Business.VK
{
    public delegate void BotMessageHandler(Message message);

    public class VkAddingBot
    {
        private VkApi _vk;
        private ulong? _ts;
        private Random _random;

        public event BotMessageHandler? OnGotMessage;

        public VkAddingBot()
        {
            _random = new Random();
            _vk = new VkApi();
            _ts = null;
        }

        public bool Authorize()
        {
            try
            {
                _vk.Authorize(new ApiAuthParams
                {
                    AccessToken = Resources.VkAccessToken,
                    Settings = Settings.All
                });

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task SendMessageAsync(string message, long userId)
        {
            await _vk.Messages.SendAsync(new MessagesSendParams
            {
                Message = message,
                UserId = userId,
                RandomId = _random.Next()
            });
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

            OnGotMessage?.Invoke(message.Message);
        }

        private async Task<BotsLongPollHistoryResponse> GetBotsLongPollHistoryResponseAsync()
        {
            var pollResponse = await _vk.Groups.GetLongPollServerAsync(ulong.Parse(Resources.GroupId));

            BotsLongPollHistoryResponse response;

            try
            {
                response = await _vk.Groups.GetBotsLongPollHistoryAsync(new BotsLongPollHistoryParams
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
    }
}
