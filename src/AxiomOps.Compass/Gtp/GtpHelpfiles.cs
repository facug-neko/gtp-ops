namespace AxiomOps.Compass.Gtp;

/// <summary>A helpfile version for a game (one per version × payout variant).</summary>
public sealed class GtpHelpfile
{
    public int VersionedDocumentId { get; set; }
    public string? Label { get; set; }
    public string? Version { get; set; }
    public string? PayoutVariant { get; set; }
}

/// <summary>
/// The easy-help validation report for a helpfile
/// (GET /easy-help/api/v1/Helpfiles/{id}/validate). <see cref="IsReadyForSubmission"/>
/// is the overall release gate.
/// </summary>
public sealed class GtpHelpfileValidation
{
    public bool IsAttributesValid { get; set; }
    public bool IsGameValid { get; set; }
    public bool IsMarketsValid { get; set; }
    public bool IsDotcomMarketValid { get; set; }
    public bool IsLanguagesValid { get; set; }
    public bool IsTopicsAndStringsValid { get; set; }
    public bool IsReadyForSubmission { get; set; }
    public bool IsSubmitted { get; set; }
    public bool HasArchivedMarkets { get; set; }
    public bool HasArchivedLanguages { get; set; }
    public bool HasArchivedStrings { get; set; }

    /// <summary>Markets present in the previous version but missing here (a regression).</summary>
    public List<string>? MissingMarketsFromPreviousVersion { get; set; }
}
