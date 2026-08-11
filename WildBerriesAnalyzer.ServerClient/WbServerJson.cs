using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace WildBerriesAnalyzer.ServerClient
{
    internal static class WbServerJson
    {
        private static readonly ILogger Log = Serilog.Log.ForContext("Area", "Http");

        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var message = ExtractMessage(body) ?? $"Сервер вернул {(int)response.StatusCode} ({response.ReasonPhrase}).";
            var path = response.RequestMessage?.RequestUri?.PathAndQuery ?? "<unknown>";

            Log.Warning(
                "API error {StatusCode} for {Path}: {Message}",
                (int)response.StatusCode,
                path,
                message);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new ArgumentException(message);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException(message);
            }

            throw new HttpRequestException(message, null, response.StatusCode);
        }

        private static string? ExtractMessage(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            var trimmed = body.Trim();

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.String)
                {
                    return document.RootElement.GetString();
                }

                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("message", out var messageProperty))
                {
                    return messageProperty.GetString();
                }
            }
            catch (JsonException)
            {
                // fall through to raw body
            }

            return trimmed.Trim('"');
        }
    }
}
