using System.Text.RegularExpressions;

namespace AxiomOps.Compass.Gtp;

public enum HelpfileIssueKind
{
    /// <summary>A content phrase that doesn't end with sentence punctuation.</summary>
    MissingPeriod,

    /// <summary>The same phrase repeated inside one topic — almost always a real defect.</summary>
    DuplicateInTopic,

    /// <summary>The same phrase in different topics — often intentional boilerplate, so advisory.</summary>
    DuplicateAcrossTopics,
}

public sealed class HelpfileContentIssue
{
    public required HelpfileIssueKind Kind { get; init; }
    public required string Topic { get; init; }
    public required string Text { get; init; }

    /// <summary>How many times the phrase appears (duplicates only).</summary>
    public int Occurrences { get; init; } = 1;

    /// <summary>What the issue is and where, ready to display.</summary>
    public string Caption => Kind switch
    {
        HelpfileIssueKind.MissingPeriod => $"Sin punto final  ·  {Topic}",
        HelpfileIssueKind.DuplicateInTopic => $"Repetida {Occurrences}× en el tópico  ·  {Topic}",
        _ => $"Repetida {Occurrences}× en  ·  {Topic}",
    };

    /// <summary>Advisory findings render muted; blocking ones stand out.</summary>
    public bool IsBlocking => Kind != HelpfileIssueKind.DuplicateAcrossTopics;
}

/// <summary>Content-quality findings for one helpfile (rules we run, not GTP's).</summary>
public sealed class HelpfileContentAnalysis
{
    public required IReadOnlyList<HelpfileContentIssue> MissingPeriod { get; init; }
    public required IReadOnlyList<HelpfileContentIssue> DuplicatesInTopic { get; init; }
    public required IReadOnlyList<HelpfileContentIssue> DuplicatesAcrossTopics { get; init; }

    /// <summary>Phrases actually checked (excludes headings and skipped patterns).</summary>
    public required int CheckedPhrases { get; init; }

    public required int SkippedPhrases { get; init; }

    public bool IsClean => MissingPeriod.Count == 0 && DuplicatesInTopic.Count == 0;
}

/// <summary>
/// Content rules the manual QA team checks by hand: every phrase ends with
/// sentence punctuation, and no duplicated phrases.
///
/// Both need exclusions to be useful — measured against a healthy helpfile
/// (Pachinko V94 v1.19), the naive rules produced ~90 false positives:
///   · topic headings never end with a period ("Payline Rules")
///   · symbol legends are "X = Y" lines ("WILD = WILD", "MINI = MINI")
///   · generated metadata ends in a placeholder ("Page generated: @pageGenerated")
///     or lives in a metadata topic ("Game manufacturer: Linko Studios")
///   · the same boilerplate phrase legitimately repeats across topics, because
///     each topic is self-contained for the player — only a repeat INSIDE one
///     topic is treated as a defect.
/// </summary>
public static class HelpfileContentRules
{
    /// <summary>
    /// Topics that hold generated metadata rather than prose: the game title, the
    /// build number and the manufacturer legal names. Nothing in here is a sentence,
    /// so the period rule doesn't apply. Newer helpfiles renamed "Game Version" to
    /// "Product Information", so both are listed.
    /// </summary>
    public static readonly IReadOnlySet<string> MetadataTopics =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Game Version",
            "Product Information",
        };

    /// <summary>Symbol legend lines like "WILD = WILD" — not sentences.</summary>
    private static readonly Regex LegendPattern = new(@"=", RegexOptions.Compiled);

    /// <summary>Lines ending in a template placeholder, e.g. "Page generated: @pageGenerated".</summary>
    private static readonly Regex EndsWithPlaceholder = new(@"@\w+\s*$", RegexOptions.Compiled);

    /// <summary>A phrase is properly terminated by '.', '!' or '?'.</summary>
    private static readonly Regex SentenceEnd = new(@"[.!?]\s*$", RegexOptions.Compiled);

    public static HelpfileContentAnalysis Analyze(IEnumerable<GtpHelpfileString> tree)
    {
        var phrases = Flatten(tree).ToList();

        // Headings are containers (they have children); only leaves carry prose.
        var content = phrases.Where(p => !p.IsHeading && !string.IsNullOrWhiteSpace(p.Text)).ToList();

        var checkable = content.Where(p => !ShouldSkip(p.Text) && !MetadataTopics.Contains(p.Topic)).ToList();
        var missingPeriod = checkable
            .Where(p => !SentenceEnd.IsMatch(p.Text))
            .Select(p => new HelpfileContentIssue
            {
                Kind = HelpfileIssueKind.MissingPeriod,
                Topic = p.Topic,
                Text = p.Text,
            })
            .ToList();

        // Duplicates: compare trimmed, case-insensitive, over content phrases.
        var groups = content
            .GroupBy(p => p.Text, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        var inTopic = new List<HelpfileContentIssue>();
        var acrossTopics = new List<HelpfileContentIssue>();

        foreach (var group in groups)
        {
            foreach (var byTopic in group.GroupBy(p => p.Topic, StringComparer.OrdinalIgnoreCase).Where(t => t.Count() > 1))
            {
                inTopic.Add(new HelpfileContentIssue
                {
                    Kind = HelpfileIssueKind.DuplicateInTopic,
                    Topic = byTopic.Key,
                    Text = group.Key,
                    Occurrences = byTopic.Count(),
                });
            }

            var topics = group.Select(p => p.Topic).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (topics.Count > 1)
            {
                acrossTopics.Add(new HelpfileContentIssue
                {
                    Kind = HelpfileIssueKind.DuplicateAcrossTopics,
                    Topic = string.Join(" · ", topics),
                    Text = group.Key,
                    Occurrences = group.Count(),
                });
            }
        }

        return new HelpfileContentAnalysis
        {
            MissingPeriod = missingPeriod,
            DuplicatesInTopic = inTopic,
            DuplicatesAcrossTopics = acrossTopics,
            CheckedPhrases = checkable.Count,
            SkippedPhrases = content.Count - checkable.Count,
        };
    }

    private static bool ShouldSkip(string text) =>
        LegendPattern.IsMatch(text) || EndsWithPlaceholder.IsMatch(text);

    private sealed record Phrase(string Text, string Topic, bool IsHeading);

    /// <summary>Flattens the tree, tagging each node with its root topic.</summary>
    private static IEnumerable<Phrase> Flatten(IEnumerable<GtpHelpfileString> nodes)
    {
        foreach (var node in nodes)
        {
            var topic = (node.Text ?? string.Empty).Trim();
            yield return new Phrase(topic, topic, IsHeading: node.Children is { Count: > 0 });

            foreach (var descendant in FlattenChildren(node.Children, topic))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<Phrase> FlattenChildren(List<GtpHelpfileString>? nodes, string topic)
    {
        foreach (var node in nodes ?? [])
        {
            yield return new Phrase((node.Text ?? string.Empty).Trim(), topic, IsHeading: node.Children is { Count: > 0 });

            foreach (var descendant in FlattenChildren(node.Children, topic))
            {
                yield return descendant;
            }
        }
    }
}
