using System;

namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    /// <summary>
    /// HTTP-ответ WB с ошибкой авторизации (401/403/498).
    /// </summary>
    public sealed class WbHttpAuthException : UnauthorizedAccessException
    {
        public WbHttpAuthException(int statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}
