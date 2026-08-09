namespace AxiomOps.Compass;

/// <summary>Thrown when the compass CLI is missing, fails, or returns unparseable output.</summary>
public sealed class CompassException(string message, string? stdout = null, string? stderr = null, Exception? inner = null)
    : Exception(message, inner)
{
    public string? Stdout { get; } = stdout;
    public string? Stderr { get; } = stderr;

    /// <summary>
    /// True when the output suggests an authentication problem — the usual fix
    /// is running `compass login` in a terminal.
    /// </summary>
    public bool LooksLikeAuthProblem
    {
        get
        {
            var text = $"{Message} {Stdout} {Stderr}";
            return text.Contains("login", StringComparison.OrdinalIgnoreCase)
                || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || text.Contains("401", StringComparison.Ordinal)
                || text.Contains("token", StringComparison.OrdinalIgnoreCase);
        }
    }
}
