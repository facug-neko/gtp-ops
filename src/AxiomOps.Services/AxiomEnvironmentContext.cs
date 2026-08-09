namespace AxiomOps.Services;

/// <summary>
/// Mutable "current environment" shared by every Axiom service. Lets the
/// application switch the target environment (base URL + api key) at runtime
/// without rebuilding the DI container. When no environment is set, requests
/// fall back to the static values in <see cref="AxiomOpsOptions"/>.
/// </summary>
public sealed class AxiomEnvironmentContext
{
    private readonly Lock _gate = new();

    /// <summary>Environment internal name, e.g. "gtp714".</summary>
    public string? EnvironmentName { get; private set; }

    /// <summary>Base URL of the current environment.</summary>
    public string? BaseUrl { get; private set; }

    /// <summary>x-api-key for the current environment (Axiom Admin auth).</summary>
    public string? ApiKey { get; private set; }

    public bool HasEnvironment => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>Raised after the environment changes (set or cleared).</summary>
    public event EventHandler? Changed;

    /// <summary>Axiom convention: https://axiomcore-app1-&lt;internalName&gt;.installprogram.eu</summary>
    public static string BuildBaseUrl(string internalName) =>
        $"https://axiomcore-app1-{internalName}.installprogram.eu";

    /// <summary>Selects an environment by internal name using the standard URL convention.</summary>
    public void SetEnvironment(string internalName, string? apiKey = null) =>
        SetEnvironment(internalName, BuildBaseUrl(internalName), apiKey);

    public void SetEnvironment(string environmentName, string baseUrl, string? apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        lock (_gate)
        {
            EnvironmentName = environmentName;
            BaseUrl = baseUrl.TrimEnd('/');
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate)
        {
            EnvironmentName = null;
            BaseUrl = null;
            ApiKey = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
