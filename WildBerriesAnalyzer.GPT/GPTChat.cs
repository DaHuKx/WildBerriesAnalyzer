using OllamaSharp;
using System.Text;

namespace WildBerriesAnalyzer.GPT
{
    public class GPTChat : IGPTChat
    {
        private bool _isInitialized;

        private OllamaApiClient _ollamaClient;
        private Chat _chat;

        public GPTChat()
        {

        }

        public void Initialize(string uri, string selectedModel)
        {
            if (_isInitialized)
            {
                return;
            }

            _ollamaClient = new OllamaApiClient(uri)
            {
                SelectedModel = selectedModel
            };

            _chat = new Chat(_ollamaClient);

            _isInitialized = true;
        }

        public async Task<string?> SendAsync(string promt)
        {
            if (!_isInitialized)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();

            await foreach (var answerToken in _chat.SendAsync(promt))
                sb.Append(answerToken);

            return sb.ToString();
        }
    }
}
