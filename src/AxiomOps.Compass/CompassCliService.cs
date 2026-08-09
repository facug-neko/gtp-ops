namespace AxiomOps.Compass;

public interface ICompassCliService
{
    /// <summary>Runs `compass --version`. Throws <see cref="CompassException"/> if the CLI is not installed.</summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs `compass portal get-environments` and returns the parsed list.</summary>
    Task<List<CompassEnvironment>> GetEnvironmentsAsync(CancellationToken cancellationToken = default);
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
}
