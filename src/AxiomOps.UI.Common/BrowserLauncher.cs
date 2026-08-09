using System.Diagnostics;

namespace AxiomOps.UI.Services;

/// <summary>Opens URLs in the default browser or in a private/incognito window.</summary>
public static class BrowserLauncher
{
    /// <summary>Returns null on success, or a user-facing error message.</summary>
    public static string? Open(string url, bool incognito)
    {
        if (!incognito)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo abrir el navegador: {ex.Message}";
            }
        }

        (string Browser, string Arguments)[] candidates =
        [
            ("chrome", $"--incognito \"{url}\""),
            ("msedge", $"-inprivate \"{url}\""),
            ("firefox", $"-private-window \"{url}\""),
        ];

        foreach (var (browser, arguments) in candidates)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = browser,
                    Arguments = arguments,
                    UseShellExecute = true,
                });
                return null;
            }
            catch
            {
                // try the next browser
            }
        }

        return "No se encontró Chrome, Edge ni Firefox para abrir en modo incógnito.";
    }
}
