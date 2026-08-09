using System.Text.Json;

namespace AxiomOps.Services;

/// <summary>
/// Turns an <see cref="AxiomApiException"/> into something a user can act on.
/// The API reports failures in two shapes:
///   - the Axiom envelope: {"success":false,"customMessage":"..."}
///   - ASP.NET ProblemDetails: {"title":"...","errors":{"Field":["msg"]}}
/// Without this, callers only see "400 BadRequest calling POST ...".
/// </summary>
public static class AxiomErrorText
{
    public static string Describe(AxiomApiException exception, int maxLength = 300)
    {
        var detail = ExtractDetail(exception.ResponseBody);
        var status = exception.StatusCode is { } code ? $"HTTP {(int)code}" : "Error";

        return detail is null
            ? Clean(exception.Message, maxLength)
            : $"{status}: {Clean(detail, maxLength)}";
    }

    private static string? ExtractDetail(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Axiom envelope.
            if (root.TryGetProperty("customMessage", out var custom) && custom.ValueKind == JsonValueKind.String)
            {
                var message = custom.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            // ProblemDetails validation errors: join "Field: message" pairs.
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var parts = errors.EnumerateObject()
                    .Select(p => $"{p.Name}: {string.Join(" ", p.Value.EnumerateArray().Select(v => v.GetString()))}")
                    .ToList();

                if (parts.Count > 0)
                {
                    return string.Join(" · ", parts);
                }
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            // Not JSON — show the raw body, it's still better than the status line.
            return body;
        }
    }

    private static string Clean(string value, int maxLength)
    {
        var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }
}
