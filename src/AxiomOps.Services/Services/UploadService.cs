using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Upload. File parameters take any readable stream plus its file name.</summary>
public interface IUploadService
{
    /// <summary>POST /Upload/FilterMap</summary>
    Task<AxiomResponse<bool>> UploadFilterMapAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/FilterMapUrl</summary>
    Task<AxiomResponse<bool>> UploadFilterMapFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GameContent</summary>
    Task<AxiomResponse<bool>> UploadGameContentAsync(Stream file, string fileName, int moduleId, int clientId, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GameContentUrl</summary>
    Task<AxiomResponse<bool>> UploadGameContentFromUrlAsync(string fileUrl, int moduleId, int clientId, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GamePresets</summary>
    Task<AxiomResponse<bool>> UploadGamePresetsAsync(Stream file, string fileName, string architecture = "x64", bool isProgressive = false, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GamePresetsUrl</summary>
    Task<AxiomResponse<bool>> UploadGamePresetsFromUrlAsync(string fileUrl, string architecture = "x64", CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GameService</summary>
    Task<AxiomResponse<bool>> UploadGameServiceAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/GameServiceUrl</summary>
    Task<AxiomResponse<bool>> UploadGameServiceFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/MobileGame — game content plus optional preset in a single call.</summary>
    Task<AxiomResponse<bool>> UploadMobileGameAsync(
        Stream gameContent,
        string gameContentFileName,
        Stream? gamePreset = null,
        string? gamePresetFileName = null,
        string architecture = "x64",
        string? filterMap = null,
        string? gameProvider = null,
        string? gameCategory = null,
        bool isProgressive = false,
        CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/PlaycheckRouteBContent</summary>
    Task<AxiomResponse<bool>> UploadPlaycheckContentAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/ProgressiveAPS</summary>
    Task<AxiomResponse<bool>> UploadProgressiveApsAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/TestDataContent — generates a test-data file from inline content.</summary>
    Task<AxiomResponse<bool>> GenerateTestDataAsync(GenerateTestDataRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/TestDataFile</summary>
    Task<AxiomResponse<bool>> UploadTestDataAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>POST /Upload/ForceGameStateFile — uploads stuck-round data for a user.</summary>
    Task<AxiomResponse<bool>> UploadForceGameStateFileAsync(Stream file, string fileName, int moduleId, int clientId, int userId, CancellationToken cancellationToken = default);
}

public sealed class UploadService(HttpClient http) : AxiomServiceBase(http), IUploadService
{
    public Task<AxiomResponse<bool>> UploadFilterMapAsync(Stream file, string fileName, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/FilterMap", FileForm("filterMapFormFile", file, fileName), cancellationToken);

    public Task<AxiomResponse<bool>> UploadFilterMapFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(WithQuery("Upload/FilterMapUrl", ("fileUrl", fileUrl)), null, cancellationToken);

    public Task<AxiomResponse<bool>> UploadGameContentAsync(Stream file, string fileName, int moduleId, int clientId, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(
            WithQuery("Upload/GameContent", ("moduleId", moduleId), ("clientId", clientId)),
            FileForm("contentFormFile", file, fileName),
            cancellationToken);

    public Task<AxiomResponse<bool>> UploadGameContentFromUrlAsync(string fileUrl, int moduleId, int clientId, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(
            WithQuery("Upload/GameContentUrl", ("fileUrl", fileUrl), ("moduleId", moduleId), ("clientId", clientId)),
            null,
            cancellationToken);

    public Task<AxiomResponse<bool>> UploadGamePresetsAsync(Stream file, string fileName, string architecture = "x64", bool isProgressive = false, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(
            WithQuery("Upload/GamePresets", ("architecture", architecture), ("isProgressive", isProgressive)),
            FileForm("presetFormFile", file, fileName),
            cancellationToken);

    public Task<AxiomResponse<bool>> UploadGamePresetsFromUrlAsync(string fileUrl, string architecture = "x64", CancellationToken cancellationToken = default) =>
        PostAsync<bool>(WithQuery("Upload/GamePresetsUrl", ("fileUrl", fileUrl), ("architecture", architecture)), null, cancellationToken);

    public Task<AxiomResponse<bool>> UploadGameServiceAsync(Stream file, string fileName, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/GameService", FileForm("serviceFormFile", file, fileName), cancellationToken);

    public Task<AxiomResponse<bool>> UploadGameServiceFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(WithQuery("Upload/GameServiceUrl", ("fileUrl", fileUrl)), null, cancellationToken);

    public Task<AxiomResponse<bool>> UploadMobileGameAsync(
        Stream gameContent,
        string gameContentFileName,
        Stream? gamePreset = null,
        string? gamePresetFileName = null,
        string architecture = "x64",
        string? filterMap = null,
        string? gameProvider = null,
        string? gameCategory = null,
        bool isProgressive = false,
        CancellationToken cancellationToken = default)
    {
        var form = FileForm("gameContentFormFile", gameContent, gameContentFileName);
        if (gamePreset is not null)
        {
            AddFile(form, "gamePresetFormFile", gamePreset, gamePresetFileName ?? "gamePreset.zip");
        }

        return PostAsync<bool>(
            WithQuery("Upload/MobileGame",
                ("architecture", architecture),
                ("filterMap", filterMap),
                ("gameProvider", gameProvider),
                ("gameCategory", gameCategory),
                ("isProgressive", isProgressive)),
            form,
            cancellationToken);
    }

    public Task<AxiomResponse<bool>> UploadPlaycheckContentAsync(Stream file, string fileName, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/PlaycheckRouteBContent", FileForm("playcheckFormFile", file, fileName), cancellationToken);

    public Task<AxiomResponse<bool>> UploadProgressiveApsAsync(Stream file, string fileName, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/ProgressiveAPS", FileForm("progressiveFormFile", file, fileName), cancellationToken);

    public Task<AxiomResponse<bool>> GenerateTestDataAsync(GenerateTestDataRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/TestDataContent", request, cancellationToken);

    public Task<AxiomResponse<bool>> UploadTestDataAsync(Stream file, string fileName, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Upload/TestDataFile", FileForm("testDataFormFile", file, fileName), cancellationToken);

    public Task<AxiomResponse<bool>> UploadForceGameStateFileAsync(Stream file, string fileName, int moduleId, int clientId, int userId, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(
            WithQuery("Upload/ForceGameStateFile", ("moduleId", moduleId), ("clientId", clientId), ("userId", userId)),
            FileForm("forceGameStateFile", file, fileName),
            cancellationToken);
}
