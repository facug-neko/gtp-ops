using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxiomOps.Services.Http;

/// <summary>
/// Tolerant <see cref="bool"/> converter for fields the API sometimes sends as
/// <c>null</c> (or a stringly-typed "true"/"false") instead of a JSON boolean
/// literal. System.Text.Json's built-in bool converter throws JsonException on
/// anything but true/false — which previously took down deserialization of the
/// entire response for one game's unset flag. Falls back to <c>false</c> for
/// anything it can't confidently read as true.
/// </summary>
public sealed class LenientBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => false,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var parsed) && parsed,
            JsonTokenType.Number => reader.TryGetInt32(out var n) && n != 0,
            _ => false,
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
