using System.Net;

namespace AxiomOps.Services;

/// <summary>
/// Thrown when the Axiom Administrator Core API returns a non-success HTTP status
/// or a payload that cannot be deserialized.
/// </summary>
public sealed class AxiomApiException : Exception
{
    public AxiomApiException(HttpStatusCode? statusCode, string requestUri, string message, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }

    public string RequestUri { get; }

    /// <summary>Raw response body, useful for diagnostics.</summary>
    public string? ResponseBody { get; }
}
