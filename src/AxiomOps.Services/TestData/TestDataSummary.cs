using System.Text.RegularExpressions;

namespace AxiomOps.Services.TestData;

/// <summary>
/// Display metadata for a testdata — the Prize/Description pair the QA play
/// repository needs — kept OUTSIDE the &lt;Test&gt; element as a trailing XML
/// comment, e.g.:
/// <code>
/// &lt;Test&gt;...&lt;/Test&gt;
/// &lt;!-- Prize: Payline3 | Description: Pays only payline 3 --&gt;
/// </code>
/// Verified against a real environment (2026-08-12): adding a real sibling element
/// to &lt;Test&gt; made the game engine stop loading the testdata. A comment placed
/// AFTER the closing tag isn't part of the document's element tree at all, so no
/// deserializer — ours or the engine's — ever sees it as content.
/// </summary>
public sealed partial record TestDataSummary(string? Prize, string? Description)
{
    public static readonly TestDataSummary Empty = new(null, null);

    public bool IsEmpty => string.IsNullOrWhiteSpace(Prize) && string.IsNullOrWhiteSpace(Description);

    /// <summary>Reads the trailing "&lt;!-- Prize: ... | Description: ... --&gt;" comment, if present.</summary>
    public static bool TryParse(string xml, out TestDataSummary summary)
    {
        var match = string.IsNullOrEmpty(xml) ? Match.Empty : CommentPattern().Match(xml);
        if (!match.Success)
        {
            summary = Empty;
            return false;
        }

        summary = new TestDataSummary(match.Groups["prize"].Value.Trim(), match.Groups["description"].Value.Trim());
        return true;
    }

    /// <summary>Removes the trailing summary comment, leaving everything else untouched.</summary>
    public static string Strip(string xml) =>
        string.IsNullOrEmpty(xml) ? xml : CommentPattern().Replace(xml, string.Empty).TrimEnd();

    /// <summary>
    /// Returns <paramref name="xml"/> with its trailing summary comment set to this
    /// instance, replacing any existing one (safe to call even if one is already
    /// present — it strips first).
    /// </summary>
    public string ApplyTo(string xml)
    {
        var body = Strip(xml);
        if (IsEmpty)
        {
            return body;
        }

        var comment = $"<!-- Prize: {Encode(Prize)} | Description: {Encode(Description)} -->";
        return string.IsNullOrEmpty(body) ? comment : $"{body}\n{comment}";
    }

    // XML comments can't contain "--", and "|" is our own field separator — both
    // get swapped for lookalikes so stray input can't break the format.
    private static string Encode(string? value) =>
        (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("--", "—")
            .Replace("|", "/")
            .Trim();

    [GeneratedRegex(@"<!--\s*Prize:\s*(?<prize>.*?)\s*\|\s*Description:\s*(?<description>.*?)\s*-->",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CommentPattern();
}
