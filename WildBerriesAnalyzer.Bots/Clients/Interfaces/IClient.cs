using WildBerriesAnalyzer.Bots.Enums;
using WildBerriesAnalyzer.Bots.Models.Messages;

namespace WildBerriesAnalyzer.Bots.Clients.Interfaces
{
    public interface IClient
    {
        event BotMessageReceivedHandler OnMessageReceived;
        Task StartListeningMessages();
        void Initialize();
        void SendMessage(BotMessage message);
        Task SendMessageAsync(BotMessage message);
        BotType BotType { get; }
    }

    public delegate void BotMessageReceivedHandler(UserMessage message);
}
