namespace AxiomOps.Services.Models;

public class HostEntry
{
    public string? HostName { get; set; }
    public string? IpAddress { get; set; }
    public bool IsMicrosoftWindows { get; set; }
    public bool IsWebServerEnabled { get; set; }
}

public class HostHealth
{
    /// <summary>Null for hosts the appliance cannot probe (e.g. non-Windows hosts).</summary>
    public bool? IsHealthy { get; set; }
    public List<NameStatus>? ServiceFailures { get; set; }
    public List<NameStatus>? WebsiteFailures { get; set; }
    public List<NameStatus>? ApplicationPoolFailures { get; set; }
}

public class HostHealthReport
{
    public HostEntry? Host { get; set; }
    public HostHealth? HostHealth { get; set; }
}

public class ApplianceHostEntries
{
    public string? EnvironmentName { get; set; }
    public List<HostEntry>? HostFileEntries { get; set; }
}

public class ApplianceState
{
    public ApplianceMetaData? ApplianceMetaData { get; set; }
    public HostHealth? ApplianceHealth { get; set; }
    public List<HostStatistics>? Statistics { get; set; }
}

public class ApplianceMetaData
{
    public string? EnvironmentName { get; set; }
    public DateTimeOffset? LastProvisionDateTime { get; set; }
    public string? AssignedOktaGroup { get; set; }
    public List<ComponentVersion>? VeyronVersions { get; set; }
    public List<ComponentVersion>? VHarnessVersions { get; set; }
    public List<NameValue>? LobbyLinks { get; set; }
    public List<NameValue>? PlaycheckLinks { get; set; }
    public List<InstalledGameSummary>? InstalledGames { get; set; }
    public List<TitanVersion>? Titan { get; set; }
}

public class InstalledGameSummary
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public string? DisplayName { get; set; }
    public int? ClientTypeId { get; set; }
}

public class HostStatistics
{
    public HostEntry? Host { get; set; }
    public HostHealth? HostHealth { get; set; }
    public HostPerformance? Performance { get; set; }
}

public class HostPerformance
{
    public HostMetrics? Metrics { get; set; }
}

public class HostMetrics
{
    public List<DiskMetrics>? Disks { get; set; }
    public CpuMetrics? Cpu { get; set; }
    public RamMetrics? Ram { get; set; }
}

/// <summary>Human-readable metric value (percentages are returned as strings).</summary>
public class MetricDetail
{
    public string? Value { get; set; }
    public string? Description { get; set; }
}

/// <summary>Absolute byte-count metric.</summary>
public class AmountMetric
{
    public long? Value { get; set; }
    public string? Description { get; set; }
}

public class PercentUsage
{
    public MetricDetail? Available { get; set; }
    public MetricDetail? Used { get; set; }
}

public class DiskMetrics
{
    public string? Name { get; set; }
    public PercentUsage? Percent { get; set; }
    public AmountMetric? TotalAvailableBytes { get; set; }
    public AmountMetric? TotalUsedBytes { get; set; }
}

public class CpuMetrics
{
    public PercentUsage? Percent { get; set; }
}

public class RamMetrics
{
    public PercentUsage? Percent { get; set; }
    public AmountMetric? TotalAvailableBytes { get; set; }
    public AmountMetric? TotalUsedBytes { get; set; }
}
