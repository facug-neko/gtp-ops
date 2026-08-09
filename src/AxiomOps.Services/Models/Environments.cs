namespace AxiomOps.Services.Models;

/// <summary>Software inventory of a single appliance host.</summary>
public class HostSoftwareProducts
{
    public string? HostName { get; set; }
    public List<SoftwarePackage>? ChocolateyPackages { get; set; }
    public List<SoftwarePackage>? ProductPackages { get; set; }
    public List<SoftwarePackage>? PoppedNugetPackages { get; set; }
    public List<SoftwarePackage>? OctopusReleases { get; set; }
    public List<SoftwarePackage>? CertificatePackages { get; set; }
    public List<SoftwarePackage>? DatabasePackages { get; set; }
}
