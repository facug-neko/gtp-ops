using System.Globalization;

namespace AxiomOps.Compass.Gtp;

/// <summary>
/// GTP portal read commands via the compass CLI. All calls need the Games Global
/// (Cognito) login; on a 401 the wrapped <see cref="CompassException"/> flags it
/// as an auth problem so the UI can prompt for `compass login`.
/// </summary>
public interface IGtpPortalService
{
    /// <summary>`compass portal get-projects-for-game --game-id X` (canonical gameId).</summary>
    Task<List<GtpProject>> GetProjectsForGameAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>`compass portal get-project --project-id X`</summary>
    Task<GtpProject?> GetProjectAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>`compass portal get-releases --project-id X`</summary>
    Task<List<GtpRelease>> GetReleasesAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// `compass certification get-deliverables-by-game-project-id --game-project-id X`.
    /// The project's V2 certification deliverables with their requirement tree.
    /// </summary>
    Task<List<GtpDeliverable>> GetDeliverablesAsync(int gameProjectId, CancellationToken cancellationToken = default);

    /// <summary>`compass certification check-pre-certification --game-project-id X` — per market×variant readiness.</summary>
    Task<GtpPreCertResult?> GetPreCertificationAsync(int gameProjectId, CancellationToken cancellationToken = default);

    /// <summary>`compass easy-help get-helpfiles-portalProjectHelpfiles-by-portalGameId --portal-game-id X`.</summary>
    Task<List<GtpHelpfile>> GetHelpfilesForGameAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>`compass easy-help ...validate --helpfile-versioned-document-id X`.</summary>
    Task<GtpHelpfileValidation?> ValidateHelpfileAsync(int versionedDocumentId, CancellationToken cancellationToken = default);

    /// <summary>`compass easy-help ...strings-missingTranslations --versioned-document-id X` — raw rows (count is what we use for now).</summary>
    Task<int> GetMissingTranslationsCountAsync(int versionedDocumentId, CancellationToken cancellationToken = default);

    /// <summary>`compass easy-help get-helpfiles-by-versionedDocumentId-strings --versioned-document-id X` — the topic/phrase tree.</summary>
    Task<List<GtpHelpfileString>> GetHelpfileStringsAsync(int versionedDocumentId, CancellationToken cancellationToken = default);
}

public sealed class GtpPortalService : IGtpPortalService
{
    private readonly CompassRunner _runner;

    public GtpPortalService() : this(new CompassRunner())
    {
    }

    public GtpPortalService(CompassRunner runner)
    {
        _runner = runner;
    }

    public Task<List<GtpProject>> GetProjectsForGameAsync(int gameId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<GtpProject>>(
            ["portal", "get-projects-for-game", "--game-id", gameId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<GtpProject?> GetProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<GtpProject?>(
            ["portal", "get-project", "--project-id", projectId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<List<GtpRelease>> GetReleasesAsync(int projectId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<GtpRelease>>(
            ["portal", "get-releases", "--project-id", projectId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<List<GtpDeliverable>> GetDeliverablesAsync(int gameProjectId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<GtpDeliverable>>(
            ["certification", "get-deliverables-by-game-project-id", "--game-project-id", gameProjectId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<GtpPreCertResult?> GetPreCertificationAsync(int gameProjectId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<GtpPreCertResult?>(
            ["certification", "check-pre-certification", "--game-project-id", gameProjectId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<List<GtpHelpfile>> GetHelpfilesForGameAsync(int gameId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<GtpHelpfile>>(
            ["easy-help", "get-helpfiles-portalProjectHelpfiles-by-portalGameId", "--portal-game-id", gameId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<GtpHelpfileValidation?> ValidateHelpfileAsync(int versionedDocumentId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<GtpHelpfileValidation?>(
            ["easy-help", "get-Helpfiles-by-helpfileVersionedDocumentId-validate", "--helpfile-versioned-document-id", versionedDocumentId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public Task<List<GtpHelpfileString>> GetHelpfileStringsAsync(int versionedDocumentId, CancellationToken cancellationToken = default) =>
        _runner.RunJsonAsync<List<GtpHelpfileString>>(
            ["easy-help", "get-helpfiles-by-versionedDocumentId-strings", "--versioned-document-id", versionedDocumentId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);

    public async Task<int> GetMissingTranslationsCountAsync(int versionedDocumentId, CancellationToken cancellationToken = default)
    {
        var rows = await _runner.RunJsonAsync<List<System.Text.Json.JsonElement>>(
            ["easy-help", "get-helpfiles-by-versionedDocumentId-strings-missingTranslations", "--versioned-document-id", versionedDocumentId.ToString(CultureInfo.InvariantCulture)],
            cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }
}
