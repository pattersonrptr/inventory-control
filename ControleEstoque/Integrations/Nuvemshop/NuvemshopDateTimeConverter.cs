using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControleEstoque.Integrations.Nuvemshop;

/// <summary>
/// Handles Nuvemshop's non-standard ISO 8601 datetime format where the UTC offset
/// lacks a colon (e.g. "2026-04-12T18:17:00-0300" instead of "-03:00").
/// System.Text.Json only supports RFC 3339 (with colon) by default.
/// </summary>
public class NuvemshopDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-dd'T'HH:mm:sszzz",    // with colon: -03:00
        "yyyy-MM-dd'T'HH:mm:sszzzz",   // without colon: -0300
        "yyyy-MM-dd'T'HH:mm:ss.FFFzzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFzzzz"
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException("Expected a non-null DateTime string.");

        if (DateTimeOffset.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.DateTime;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
            return dto.DateTime;

        throw new JsonException($"Unable to parse DateTime value: '{value}'");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
    }
}
