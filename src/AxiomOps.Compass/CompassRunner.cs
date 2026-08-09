using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AxiomOps.Compass;

/// <summary>
/// Runs the Games Global `compass` CLI and parses its output. Compass handles
/// its own auth (Okta/Cognito, persisted with auto-refresh); we only spawn it
/// and read stdout. Precondition: the user ran `compass login` once. Shared by
/// every compass-backed service (Axiom environments and GTP portal).
/// </summary>
public sealed class CompassRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string? _cachedBin;

    /// <summary>Runs a compass command and returns its raw stdout.</summary>
    public async Task<string> RunTextAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var (stdout, _) = await RunAsync(args, cancellationToken).ConfigureAwait(false);
        return stdout;
    }

    /// <summary>Runs a compass command and deserializes its JSON stdout.</summary>
    public async Task<T> RunJsonAsync<T>(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var (stdout, stderr) = await RunAsync(args, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new CompassException(
                $"compass {string.Join(' ', args)} returned no output. " +
                "If this is an auth problem, run `compass login` in a terminal.",
                stdout, stderr);
        }

        var json = ExtractJsonPayload(stdout, args);

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new CompassException($"compass {string.Join(' ', args)} returned null JSON.", stdout, stderr);
        }
        catch (JsonException ex)
        {
            throw new CompassException(
                $"Could not parse compass {string.Join(' ', args)} output as JSON.",
                stdout, stderr, ex);
        }
    }

    /// <summary>
    /// Isolates the JSON payload: compass sometimes prints an "Update available"
    /// banner before/after the JSON. Find the first '{' or '[', then trim
    /// trailing lines until it parses.
    /// </summary>
    private static string ExtractJsonPayload(string stdout, IReadOnlyList<string> args)
    {
        var text = stdout.Trim();
        var starts = new[] { text.IndexOf('{'), text.IndexOf('[') }.Where(i => i >= 0).ToArray();

        if (starts.Length == 0)
        {
            throw new CompassException(
                $"No JSON payload found in compass {string.Join(' ', args)} output. " +
                "If compass is asking you to log in, run `compass login` in a terminal.",
                stdout);
        }

        var slice = text[starts.Min()..];

        try
        {
            using var _ = JsonDocument.Parse(slice);
            return slice;
        }
        catch (JsonException)
        {
            var lines = slice.Split('\n');
            for (var i = lines.Length - 1; i > 0; i--)
            {
                var candidate = string.Join('\n', lines[..i]);
                try
                {
                    using var _ = JsonDocument.Parse(candidate);
                    return candidate;
                }
                catch (JsonException)
                {
                    // keep trimming
                }
            }

            throw;
        }
    }

    private async Task<(string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var bin = ResolveCompassBin();

        // .cmd/.bat scripts cannot be spawned directly (same restriction that
        // hit Node with CVE-2024-27980) — route them through cmd.exe.
        var isBatch = bin.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                      || bin.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        var psi = new ProcessStartInfo
        {
            FileName = isBatch ? "cmd.exe" : bin,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (isBatch)
        {
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/s");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(bin);
        }

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new CompassException(
                "Could not start the compass CLI. Install it with `npm install -g <compass-cli.tgz>` " +
                "and confirm `compass --version` works in a terminal.",
                inner: ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new CompassException($"compass {string.Join(' ', args)} timed out after 90s.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode > 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new CompassException(
                $"compass {string.Join(' ', args)} failed with exit {process.ExitCode}: {Truncate(detail, 400)}",
                stdout, stderr);
        }

        return (stdout, stderr);
    }

    /// <summary>
    /// Resolves the compass binary via `where`. npm global bins on Windows are
    /// `.cmd` wrappers (%APPDATA%\npm\compass.cmd). Cached per instance.
    /// </summary>
    private string ResolveCompassBin()
    {
        if (_cachedBin is not null)
        {
            return _cachedBin;
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? ["compass.cmd", "compass.exe", "compass"]
            : ["compass"];

        foreach (var name in candidates)
        {
            var resolved = Where(name);
            if (resolved is not null)
            {
                _cachedBin = resolved;
                return resolved;
            }
        }

        throw new CompassException(
            "compass binary not found on PATH. Install with `npm install -g <compass-cli.tgz>` " +
            "and confirm `compass --version` works in a fresh terminal.");
    }

    private static string? Where(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where.exe" : "which",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(name);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var firstLine = output.Split('\n').FirstOrDefault()?.Trim();
            return process.ExitCode == 0 && !string.IsNullOrEmpty(firstLine) ? firstLine : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }
}
