namespace AxiomOps.Services;

/// <summary>
/// Configuration for the Axiom Administrator Core API client.
/// </summary>
public sealed class AxiomOpsOptions
{
    /// <summary>
    /// Default base URL, e.g. "https://axiomcore-app1-gtp714.installprogram.eu".
    /// Optional — <see cref="AxiomEnvironmentContext"/> takes precedence and allows
    /// switching environments at runtime.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Static bearer token. Ignored when <see cref="AccessTokenProvider"/> is set.</summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Default x-api-key (Axiom Admin auth). The per-environment key in
    /// <see cref="AxiomEnvironmentContext"/> takes precedence.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Callback invoked before each request to obtain a bearer token.
    /// Use this to plug in an Okta/OAuth refresh flow so tokens stay valid on long sessions.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? AccessTokenProvider { get; set; }

    /// <summary>Request timeout applied to the underlying <see cref="HttpClient"/>.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
