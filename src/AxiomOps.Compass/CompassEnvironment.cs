namespace AxiomOps.Compass;

/// <summary>Environment record returned by `compass portal get-environments`.</summary>
public class CompassEnvironment
{
    public int Id { get; set; }
    public string InternalName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Type { get; set; }

    /// <summary>Admin portal URL, e.g. https://admin-app1-gtp555.installprogram.eu</summary>
    public string? Hostname { get; set; }

    /// <summary>Axiom Administrator Core API URL, e.g. https://axiomcore-app1-gtp555.installprogram.eu</summary>
    public string? HealthHostname { get; set; }

    public string? EnvironmentVersion { get; set; }
    public string? Status { get; set; }

    public bool IsAxiom => string.Equals(Type, "Axiom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Base URL for the Axiom Administrator Core API: the portal-provided
    /// healthHostname when present, otherwise the axiomcore-app1 convention.
    /// </summary>
    public string AxiomCoreBaseUrl =>
        !string.IsNullOrWhiteSpace(HealthHostname)
            ? HealthHostname.TrimEnd('/')
            : $"https://axiomcore-app1-{InternalName}.installprogram.eu";

    public override string ToString() => $"{InternalName} — {Name}";
}
