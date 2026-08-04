using GigaChatAdapter;
using GigaChatAdapter.Auth;
using WildBerriesAnalyzer.VkAddProductBot.Properties;

namespace WildBerriesAnalyzer.VkAddProductBot
{
    public class GigaChater
    {
        private Authorization _authorization;
        private Completion _completion;

        public GigaChater()
        {

        }

        public async Task<bool> AuthorizeAsync()
        {
            _authorization = new Authorization(Resources.GigaChatKey, RateScope.GIGACHAT_API_PERS);

            var result = await _authorization.SendRequest();

            if (result.AuthorizationSuccess)
            {
                _completion = new Completion();

                _ = SendMessage("Я буду давать тебе на вход названия товаров, твоя задача ответить, является ли данный товар 18+\n\n" +
                                "Отвечай только:\n" +
                                "true, если является\n" +
                                "false, если не является");
            }

            return result.AuthorizationSuccess;
        }

        public async Task<bool> ProductIsApproved(string name)
        {
            await _authorization.UpdateToken();

            if (bool.TryParse(await SendMessage(name), out bool result))
            {
                return result;
            }
            else
            {
                return false;
            }
        }

        private async Task<string> SendMessage(string message)
        {
            var result = await _completion.SendRequest(_authorization.LastResponse.GigaChatAuthorizationResponse!.AccessToken, message);

            if (result.RequestSuccessed)
            {
                return result.GigaChatCompletionResponse.Choices.Last().Message.Content;
            }
            else
            {
                throw new Exception(result.ErrorTextIfFailed);
            }
        }
    }
}
