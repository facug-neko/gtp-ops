namespace AxiomOps.Services.Models;

/// <summary>Generic name/value pair used by several endpoints (links, folders...).</summary>
public class NameValue
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}

/// <summary>Generic name/status pair (services, websites, app pools, health failures).</summary>
public class NameStatus
{
    public string? Name { get; set; }
    public string? Status { get; set; }
}

/// <summary>Installed software package (Chocolatey, NuGet, Octopus...).</summary>
public class SoftwarePackage
{
    public string? Name { get; set; }
    public string? Version { get; set; }
}

/// <summary>Versioned platform component (Veyron, VHarness...).</summary>
public class ComponentVersion
{
    public string? DisplayName { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Version { get; set; }
}
