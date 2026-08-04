using System;

namespace WildBerriesAnalyzer.Business.Models
{
    public sealed class AccountProfile
    {
        public required int UserId { get; init; }

        public string? Login { get; init; }

        public string? VkId { get; init; }

        public bool IsVkLinked { get; init; }
    }

    public sealed class VkLinkCodeResult
    {
        public required string Code { get; init; }

        public required DateTime ExpiresAt { get; init; }

        public required string Instruction { get; init; }
    }

    public sealed class VkLinkConfirmResult
    {
        public bool Success { get; init; }

        public required string Message { get; init; }
    }
}
