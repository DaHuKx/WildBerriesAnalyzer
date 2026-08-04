namespace WildBerriesAnalyzer.Server.Services.Auth
{
    public sealed class PendingRegistration
    {
        public required string RegistrationId { get; init; }

        public required string Login { get; init; }

        public required string PasswordHash { get; init; }

        public required string VkId { get; init; }

        public required string Code { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int FailedAttempts { get; set; }

        public DateTime CreatedAtUtc { get; init; }
    }
}
