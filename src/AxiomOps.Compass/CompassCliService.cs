namespace AxiomOps.Compass;

public interface ICompassCliService
{
    /// <summary>Runs `compass --version`. Throws <see cref="CompassException"/> if the CLI is not installed.</summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs `compass portal get-environments` and returns the parsed list.</summary>
    Task<List<CompassEnvironment>> GetEnvironmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs `compass portal reboot-appliance --environment-id &lt;id&gt;` — restarts every
    /// service on the environment (POST /api/v1/environments/{id}/reboot on the Portal).
    /// Requires a Portal (GGL) login, same as GetEnvironmentsAsync. Throws
    /// <see cref="CompassException"/> on failure; a normal return means compass exited 0.
    /// </summary>
    Task RebootEnvironmentAsync(int environmentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Axiom-facing compass commands (environment listing). Thin wrapper over
/// <see cref="CompassRunner"/>, which does the actual spawning and parsing.
/// </summary>
public sealed class CompassCliService : ICompassCliService
{
    private readonly CompassRunner _runner = new();

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var stdout = await _runner.RunTextAsync(["--version"], cancellationToken).ConfigureAwait(false);
        return stdout.Trim();
    }

    public Task<List<CompassEnvironment>> GetEnvironmentsAsync(CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<CompassEnvironment>>(["portal", "get-environments"], cancellationToken);

    public Task RebootEnvironmentAsync(int environmentId, CancellationToken cancellationToken = default) =>
        _runner.RunTextAsync(
            ["portal", "reboot-appliance", "--environment-id", environmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            cancellationToken);
}
