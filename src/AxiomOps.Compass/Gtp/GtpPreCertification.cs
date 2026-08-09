namespace AxiomOps.Compass.Gtp;

/// <summary>
/// Pre-certification check for a project (GET /api/v1/certificationchecker/{gameProjectId}):
/// per market × payout variant, whether it is deployable/certified right now.
/// </summary>
public sealed class GtpPreCertResult
{
    public int GameId { get; set; }
    public List<GtpPreCertMarket>? Markets { get; set; }
}

public sealed class GtpPreCertMarket
{
    public string? Variant { get; set; }
    public int ModuleId { get; set; }
    public int ClientId { get; set; }
    public string? Market { get; set; }
    public string? MarketCode { get; set; }

    /// <summary>Regulated, CertificatePresent, CertificateMissing, ServiceVersionMatch/Mismatch, HashMatch, Unregulated.</summary>
    public List<string>? Characteristics { get; set; }

    /// <summary>DoesNotRequireCertification / RequiresCertificationNewMarket / RequiresCertificationServiceVersionChange.</summary>
    public string? Decision { get; set; }

    public GtpPreCertMetadata? Metadata { get; set; }

    public bool RequiresCertification =>
        !string.Equals(Decision, "DoesNotRequireCertification", StringComparison.OrdinalIgnoreCase);

    public bool Has(string characteristic) =>
        Characteristics?.Any(c => string.Equals(c, characteristic, StringComparison.OrdinalIgnoreCase)) ?? false;
}

public sealed class GtpPreCertMetadata
{
    public string? CurrentServiceVersion { get; set; }
    public string? ActiveServiceVersion { get; set; }
}
