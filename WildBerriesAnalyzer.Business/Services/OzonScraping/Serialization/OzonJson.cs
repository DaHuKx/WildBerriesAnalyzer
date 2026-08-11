using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

public static class OzonJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions(writeIndented: false);
    public static readonly JsonSerializerOptions PrettyOptions = CreateOptions(writeIndented: true);

    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = writeIndented,
            // Иначе кириллица уходит в \uXXXX — это не битая кодировка, а экранирование JSON.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static string SerializePretty<T>(T value) =>
        JsonSerializer.Serialize(value, PrettyOptions);
}
