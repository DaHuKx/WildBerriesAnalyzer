namespace WildBerriesAnalyzer.Server.Services.VkBot
{
    public interface IVkCommunityMessenger
    {
        /// <summary>
        /// Резолвит числовой VK user id из ссылки профиля или screen_name.
        /// </summary>
        Task<string> ResolveUserIdAsync(string profileUrlOrScreenName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Пытается отправить личное сообщение от имени сообщества.
        /// </summary>
        Task<bool> TrySendMessageAsync(string vkUserId, string text, CancellationToken cancellationToken = default);

        string BotChatUrl { get; }

        bool IsConfigured { get; }
    }
}
