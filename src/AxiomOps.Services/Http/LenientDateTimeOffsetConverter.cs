using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxiomOps.Services.Http;

/// <summary>
/// Tolerant <see cref="DateTimeOffset"/>? converter for fields the API sometimes
/// sends WITHOUT a timezone offset (e.g. a folder's dateModified, since a folder
/// has no real "last modified" and the API sends a sentinel like
/// "0001-01-01T00:00:00" instead of "...Z"). System.Text.Json's built-in
/// DateTimeOffset converter REQUIRES an explicit offset and throws JsonException
/// otherwise — which previously took down deserialization of the entire response
/// (one folder's placeholder date broke the whole file tree). This converter
/// assumes UTC when no offset is present, and falls back to null for anything it
/// still can't parse, instead of throwing.
/// </summary>
public sealed class LenientDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value)
            ? value
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value);
        }
    }
}
