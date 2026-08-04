namespace WildBerriesAnalyzer.Business.Models
{
    public class RegisterResult
    {
        /// <summary>
        /// Идентификатор незавершённой регистрации (для подтверждения кодом из VK).
        /// </summary>
        public string RegistrationId { get; init; } = string.Empty;

        public string Login { get; init; } = string.Empty;

        public string VkId { get; init; } = string.Empty;

        /// <summary>
        /// Нужно ввести код из VK, чтобы завершить регистрацию.
        /// </summary>
        public bool RequiresVkVerification { get; init; }

        /// <summary>
        /// Удалось ли отправить проверочный код в личку VK.
        /// </summary>
        public bool VerificationMessageSent { get; init; }

        /// <summary>
        /// Ссылка на чат с сообществом/ботом.
        /// </summary>
        public string BotChatUrl { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }
}
