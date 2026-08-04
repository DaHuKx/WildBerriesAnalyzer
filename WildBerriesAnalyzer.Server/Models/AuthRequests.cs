namespace WildBerriesAnalyzer.Server.Models
{
    public class LoginRequest
    {
        public string Login { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Login { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string VkProfileUrl { get; set; } = string.Empty;
    }

    public class ConfirmRegisterRequest
    {
        public string RegistrationId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public class ResendRegisterCodeRequest
    {
        public string RegistrationId { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthTokensResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;
    }
}
