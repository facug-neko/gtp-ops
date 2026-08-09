namespace AxiomOps.Compass.Gtp;

/// <summary>
/// A helpfile string node. The endpoint returns a tree: root nodes are topics
/// (headings, with children); their children are the content phrases.
/// </summary>
public sealed class GtpHelpfileString
{
    public int DocumentStringId { get; set; }
    public int? ParentDocumentStringId { get; set; }
    public string? Text { get; set; }
    public int TranslationId { get; set; }
    public int SortKey { get; set; }
    public List<GtpHelpfileString>? Children { get; set; }
}
