using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AxiomOps.Services.Http;

/// <summary>
/// Shared plumbing for every Axiom service: request dispatch, envelope deserialization,
/// query-string building and multipart file uploads.
/// </summary>
public abstract class AxiomServiceBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected HttpClient Http { get; }

    protected AxiomServiceBase(HttpClient http)
    {
        Http = http;
    }

    protected Task<AxiomResponse<T>> GetAsync<T>(string uri, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, uri, null, cancellationToken);

    protected Task<AxiomResponse<T>> PostAsync<T>(string uri, object? body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, uri, body, cancellationToken);

    protected Task<AxiomResponse<T>> PatchAsync<T>(string uri, object? body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Patch, uri, body, cancellationToken);

    protected Task<AxiomResponse<T>> DeleteAsync<T>(string uri, object? body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Delete, uri, body, cancellationToken);

    protected async Task<AxiomResponse<T>> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);

        if (body is HttpContent content)
        {
            request.Content = content;
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new AxiomApiException(
                response.StatusCode,
                uri,
                $"{(int)response.StatusCode} {response.StatusCode} calling {method} {uri}.",
                raw);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AxiomResponse<T> { Success = true };
        }

        try
        {
            return JsonSerializer.Deserialize<AxiomResponse<T>>(raw, JsonOptions)
                   ?? new AxiomResponse<T> { Success = true };
        }
        catch (JsonException ex)
        {
            throw new AxiomApiException(response.StatusCode, uri, $"Could not deserialize response from {method} {uri}.", raw, ex);
        }
    }

    /// <summary>Appends the non-null parameters to <paramref name="path"/> as a query string.</summary>
    protected static string WithQuery(string path, params (string Name, object? Value)[] parameters)
    {
        var builder = new StringBuilder(path);
        var separator = '?';

        foreach (var (name, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            builder.Append(separator)
                   .Append(Uri.EscapeDataString(name))
                   .Append('=')
                   .Append(Uri.EscapeDataString(FormatQueryValue(value)));
            separator = '&';
        }

        return builder.ToString();
    }

    private static string FormatQueryValue(object value) => value switch
    {
        bool b => b ? "true" : "false",
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Builds a multipart form body with a single file field.</summary>
    protected static MultipartFormDataContent FileForm(string fieldName, Stream file, string fileName)
    {
        var form = new MultipartFormDataContent();
        AddFile(form, fieldName, file, fileName);
        return form;
    }

    protected static void AddFile(MultipartFormDataContent form, string fieldName, Stream file, string fileName)
    {
        var content = new StreamContent(file);
        form.Add(content, fieldName, fileName);
    }
}
