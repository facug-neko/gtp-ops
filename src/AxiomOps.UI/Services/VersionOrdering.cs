using System.Globalization;

namespace AxiomOps.UI.Services;

/// <summary>
/// Orders version strings newest-first. Game content and Titan versions come from
/// different endpoints with no guaranteed order, so we can't just take the first
/// row — we sort by the dotted numeric segments (1.20.0 &gt; 1.9.0), falling back
/// to an ordinal compare for anything non-numeric.
/// </summary>
public static class VersionOrdering
{
    /// <summary>Newest-first copy of <paramref name="versions"/> (nulls/blanks dropped).</summary>
    public static List<string> Descending(IEnumerable<string?> versions) =>
        [.. versions
            .OfType<string>()
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .OrderByDescending(v => v, Comparer)];

    /// <summary>The newest version, or null when the sequence is empty.</summary>
    public static string? Latest(IEnumerable<string?> versions) => Descending(versions).FirstOrDefault();

    public static readonly IComparer<string> Comparer = new VersionComparer();

    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var left = Segments(x);
            var right = Segments(y);
            var count = Math.Max(left.Length, right.Length);

            for (var i = 0; i < count; i++)
            {
                var l = i < left.Length ? left[i] : 0;
                var r = i < right.Length ? right[i] : 0;
                if (l != r) return l.CompareTo(r);
            }

            // Same numeric parts (e.g. "1.0" vs "1.0.0"): keep it stable/ordinal.
            return string.CompareOrdinal(x, y);
        }

        /// <summary>Leading numeric run of each dot-separated part; non-numeric parts count as 0.</summary>
        private static long[] Segments(string version)
        {
            var parts = version.Split('.', '-', '_');
            var result = new long[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var digits = new string([.. parts[i].TakeWhile(char.IsDigit)]);
                result[i] = long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
            }

            return result;
        }
    }
}
