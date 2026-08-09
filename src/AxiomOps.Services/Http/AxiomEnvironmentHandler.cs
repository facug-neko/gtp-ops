using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace AxiomOps.Services.Http;

/// <summary>
/// Routes every request to the currently selected environment and attaches
/// authentication. Clients are created against a placeholder base address so
/// the target host can change at runtime via <see cref="AxiomEnvironmentContext"/>.
///
/// Auth precedence: the context's x-api-key (per-environment, the mechanism
/// Axiom Admin actually accepts — see axiom-compass task #36) plus, when
/// configured, the bearer token from <see cref="AxiomOpsOptions"/>.
/// </summary>
internal sealed class AxiomEnvironmentHandler(
    AxiomEnvironmentContext context,
    IOptions<AxiomOpsOptions> options) : DelegatingHandler
{
    internal const string PlaceholderHost = "axiomops.invalid";
    internal static readonly Uri PlaceholderBaseAddress = new($"https://{PlaceholderHost}/");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (request.RequestUri is { } uri && string.Equals(uri.Host, PlaceholderHost, StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = context.BaseUrl
                          ?? (string.IsNullOrWhiteSpace(opts.BaseUrl) ? null : opts.BaseUrl.TrimEnd('/'));

            if (baseUrl is null)
            {
                throw new InvalidOperationException(
                    "No Axiom environment selected. Call AxiomEnvironmentContext.SetEnvironment(...) " +
                    "or configure AxiomOpsOptions.BaseUrl.");
            }

            request.RequestUri = new Uri(new Uri(baseUrl + "/"), uri.PathAndQuery.TrimStart('/'));
        }

        var apiKey = context.ApiKey ?? opts.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Remove("x-api-key");
            request.Headers.Add("x-api-key", apiKey);
        }

        var token = opts.AccessTokenProvider is not null
            ? await opts.AccessTokenProvider(cancellationToken).ConfigureAwait(false)
            : opts.AccessToken;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
