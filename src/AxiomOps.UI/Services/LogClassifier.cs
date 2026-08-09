using System.Text.RegularExpressions;

namespace AxiomOps.UI.Services;

/// <summary>Severity of a log line, worst-first so numeric comparison picks the worst.</summary>
public enum LogSeverity
{
    Error,
    Warning,
    Info,
    Debug,
    Trace,
    None,
}

/// <summary>One parsed line of a log file.</summary>
public sealed class LogLine
{
    public required int Number { get; init; }
    public required string Text { get; init; }
    public required LogSeverity Severity { get; init; }

    /// <summary>A line that continues the entry started above (detail/stack-trace line).</summary>
    public bool IsContinuation { get; init; }

    public bool IsProblem => Severity is LogSeverity.Error or LogSeverity.Warning;
}

/// <summary>
/// Classifies raw log text so the viewer can colour lines and count problems.
///
/// The unit is the log ENTRY, not the physical line: an entry starts on a line with
/// a leading timestamp and absorbs every following line until the next timestamp, so
/// a message plus its whole stack trace is one entry. The entry's severity is the
/// worst level token found across its lines, and every line inherits it — so the
/// error/warning counts reflect distinct events while the full block still lights up.
/// Logs without timestamps fall back to a line-based heuristic (indent / stack frame).
/// </summary>
public static partial class LogClassifier
{
    public static IReadOnlyList<LogLine> Parse(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var rawLines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        return rawLines.Any(EntryStartRegex().IsMatch)
            ? ParseByEntry(rawLines)
            : ParseByHeuristic(rawLines);
    }

    /// <summary>Timestamped logs: group lines into entries and rate each entry as a whole.</summary>
    private static List<LogLine> ParseByEntry(string[] rawLines)
    {
        var result = new List<LogLine>(rawLines.Length);

        var index = 0;
        while (index < rawLines.Length)
        {
            var start = index;
            index++;

            // Absorb continuation lines until the next entry start.
            while (index < rawLines.Length && !EntryStartRegex().IsMatch(rawLines[index]))
            {
                index++;
            }

            // Entry severity = worst token across its lines (the level is usually on
            // the first line, but a detail line may carry it instead).
            var severity = LogSeverity.None;
            for (var i = start; i < index; i++)
            {
                severity = Worst(severity, ClassifyToken(rawLines[i]));
            }

            for (var i = start; i < index; i++)
            {
                var text = rawLines[i];
                var lineSeverity = string.IsNullOrWhiteSpace(text) ? LogSeverity.None : severity;
                result.Add(new LogLine
                {
                    Number = i + 1,
                    Text = text,
                    Severity = lineSeverity,
                    IsContinuation = i != start,
                });
            }
        }

        return result;
    }

    /// <summary>No timestamps: rate each line, folding stack-trace lines into the entry above.</summary>
    private static List<LogLine> ParseByHeuristic(string[] rawLines)
    {
        var result = new List<LogLine>(rawLines.Length);

        var lastPrimary = LogSeverity.None;
        for (var i = 0; i < rawLines.Length; i++)
        {
            var text = rawLines[i];
            var severity = ClassifyToken(text);

            if (severity == LogSeverity.None && IsContinuationLine(text) && lastPrimary is LogSeverity.Error or LogSeverity.Warning)
            {
                result.Add(new LogLine { Number = i + 1, Text = text, Severity = lastPrimary, IsContinuation = true });
                continue;
            }

            if (severity != LogSeverity.None)
            {
                lastPrimary = severity;
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                lastPrimary = LogSeverity.None;
            }

            result.Add(new LogLine { Number = i + 1, Text = text, Severity = severity });
        }

        return result;
    }

    private static LogSeverity Worst(LogSeverity a, LogSeverity b) => (int)a <= (int)b ? a : b;

    /// <summary>Severity from level tokens on the line itself (None if none present).</summary>
    private static LogSeverity ClassifyToken(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return LogSeverity.None;
        }

        if (ErrorRegex().IsMatch(line)) return LogSeverity.Error;
        if (WarnRegex().IsMatch(line)) return LogSeverity.Warning;
        if (InfoRegex().IsMatch(line)) return LogSeverity.Info;
        if (DebugRegex().IsMatch(line)) return LogSeverity.Debug;
        if (TraceRegex().IsMatch(line)) return LogSeverity.Trace;
        return LogSeverity.None;
    }

    /// <summary>Indented lines, ".NET" stack frames, and inner-exception markers.</summary>
    private static bool IsContinuationLine(string line) =>
        !string.IsNullOrEmpty(line)
        && (char.IsWhiteSpace(line[0]) || ContinuationRegex().IsMatch(line));

    // A new log entry begins with a timestamp: ISO "2026-07-27 17:32:01", a bare
    // clock "05:03:00[.123]", or a "27/07/2026" style date, optionally bracketed.
    [GeneratedRegex(@"^\s*\[?(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}|\d{2}:\d{2}:\d{2}(\.\d+)?|\d{2}[/-]\d{2}[/-]\d{4})", RegexOptions.CultureInvariant)]
    private static partial Regex EntryStartRegex();

    // FATAL/CRITICAL/SEVERE and bare "Exception"/"Unhandled" all read as errors.
    [GeneratedRegex(@"(?i)\b(ERROR|ERR|FATAL|CRITICAL|SEVERE|EXCEPTION|UNHANDLED)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"(?i)\b(WARN|WARNING)\b", RegexOptions.CultureInvariant)]
    private static partial Regex WarnRegex();

    [GeneratedRegex(@"(?i)\b(INFO|INFORMATION)\b", RegexOptions.CultureInvariant)]
    private static partial Regex InfoRegex();

    [GeneratedRegex(@"(?i)\bDEBUG\b", RegexOptions.CultureInvariant)]
    private static partial Regex DebugRegex();

    [GeneratedRegex(@"(?i)\b(TRACE|VERBOSE)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TraceRegex();

    [GeneratedRegex(@"^\s*(at\s|---|--->|\}|\{|System\.|Microsoft\.)", RegexOptions.CultureInvariant)]
    private static partial Regex ContinuationRegex();
}
