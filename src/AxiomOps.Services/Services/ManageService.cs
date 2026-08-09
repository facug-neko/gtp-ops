using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Manage (Content, IIS and Windows services).</summary>
public interface IManageService
{
    // ----- Content -----

    /// <summary>GET /Manage/Content/ManageableFolders</summary>
    Task<AxiomResponse<List<NameValue>>> GetManageableFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Manage/Content/FileContent</summary>
    Task<AxiomResponse<FileContent>> GetFileContentAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/Content/FileContent</summary>
    Task<AxiomResponse<bool>> SetFileContentAsync(FileContent request, CancellationToken cancellationToken = default);

    /// <summary>GET /Manage/Content/FileFolderView</summary>
    Task<AxiomResponse<List<FileFolderNode>>> GetFileFolderViewAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>GET /Manage/Content/ContentDeliveryNetworkSetting</summary>
    Task<AxiomResponse<bool>> GetCdnSettingAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/Content/ContentDeliveryNetworkSetting</summary>
    Task<AxiomResponse<bool>> SetCdnSettingAsync(bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>DELETE /Manage/Content/File</summary>
    Task<AxiomResponse<bool>> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>DELETE /Manage/Content/Folder</summary>
    Task<AxiomResponse<bool>> DeleteFolderAsync(string folderPath, bool deleteParentFolder = false, CancellationToken cancellationToken = default);

    // ----- IIS: application pools -----

    /// <summary>GET /Manage/IIS/AppPool/Info</summary>
    Task<AxiomResponse<List<NameStatus>>> GetAppPoolInfoAsync(string hostName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/AppPool/Start</summary>
    Task<AxiomResponse<bool>> StartAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/AppPool/Stop</summary>
    Task<AxiomResponse<bool>> StopAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/AppPool/Recycle</summary>
    Task<AxiomResponse<bool>> RecycleAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default);

    // ----- IIS: websites -----

    /// <summary>GET /Manage/IIS/Website/Info</summary>
    Task<AxiomResponse<List<WebsiteInfo>>> GetWebsiteInfoAsync(string hostName, CancellationToken cancellationToken = default);

    /// <summary>GET /Manage/IIS/Website/State</summary>
    Task<AxiomResponse<string>> GetWebsiteStateAsync(string hostName, string siteName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/WebSite/Start</summary>
    Task<AxiomResponse<bool>> StartWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/Website/Stop</summary>
    Task<AxiomResponse<bool>> StopWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/IIS/Website/Restart</summary>
    Task<AxiomResponse<bool>> RestartWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default);

    // ----- Windows services -----

    /// <summary>GET /Manage/Service/Info</summary>
    Task<AxiomResponse<List<WindowsServiceInfo>>> GetServiceInfoAsync(string hostName, CancellationToken cancellationToken = default);

    /// <summary>GET /Manage/Service/State</summary>
    Task<AxiomResponse<string>> GetServiceStateAsync(string hostName, string serviceName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/Service/Start</summary>
    Task<AxiomResponse<ServiceActionResult>> StartServiceAsync(string hostName, string serviceName, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Manage/Service/Stop</summary>
    Task<AxiomResponse<ServiceActionResult>> StopServiceAsync(string hostName, string serviceName, CancellationToken cancellationToken = default);
}

public sealed class ManageService(HttpClient http) : AxiomServiceBase(http), IManageService
{
    public Task<AxiomResponse<List<NameValue>>> GetManageableFoldersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<NameValue>>("Manage/Content/ManageableFolders", cancellationToken);

    public Task<AxiomResponse<FileContent>> GetFileContentAsync(string filePath, CancellationToken cancellationToken = default) =>
        GetAsync<FileContent>(WithQuery("Manage/Content/FileContent", ("filePath", filePath)), cancellationToken);

    public Task<AxiomResponse<bool>> SetFileContentAsync(FileContent request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Manage/Content/FileContent", request, cancellationToken);

    public Task<AxiomResponse<List<FileFolderNode>>> GetFileFolderViewAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        GetAsync<List<FileFolderNode>>(WithQuery("Manage/Content/FileFolderView", ("directoryPath", directoryPath)), cancellationToken);

    public Task<AxiomResponse<bool>> GetCdnSettingAsync(CancellationToken cancellationToken = default) =>
        GetAsync<bool>("Manage/Content/ContentDeliveryNetworkSetting", cancellationToken);

    public Task<AxiomResponse<bool>> SetCdnSettingAsync(bool isEnabled, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/Content/ContentDeliveryNetworkSetting", ("isEnabled", isEnabled)), null, cancellationToken);

    public Task<AxiomResponse<bool>> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default) =>
        DeleteAsync<bool>(WithQuery("Manage/Content/File", ("filePath", filePath)), null, cancellationToken);

    public Task<AxiomResponse<bool>> DeleteFolderAsync(string folderPath, bool deleteParentFolder = false, CancellationToken cancellationToken = default) =>
        DeleteAsync<bool>(WithQuery("Manage/Content/Folder", ("folderPath", folderPath), ("deleteParentFolder", deleteParentFolder)), null, cancellationToken);

    public Task<AxiomResponse<List<NameStatus>>> GetAppPoolInfoAsync(string hostName, CancellationToken cancellationToken = default) =>
        GetAsync<List<NameStatus>>(WithQuery("Manage/IIS/AppPool/Info", ("hostName", hostName)), cancellationToken);

    public Task<AxiomResponse<bool>> StartAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/AppPool/Start", ("hostName", hostName), ("applicationPoolId", applicationPoolId)), null, cancellationToken);

    public Task<AxiomResponse<bool>> StopAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/AppPool/Stop", ("hostName", hostName), ("applicationPoolId", applicationPoolId)), null, cancellationToken);

    public Task<AxiomResponse<bool>> RecycleAppPoolAsync(string hostName, string applicationPoolId, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/AppPool/Recycle", ("hostName", hostName), ("applicationPoolId", applicationPoolId)), null, cancellationToken);

    public Task<AxiomResponse<List<WebsiteInfo>>> GetWebsiteInfoAsync(string hostName, CancellationToken cancellationToken = default) =>
        GetAsync<List<WebsiteInfo>>(WithQuery("Manage/IIS/Website/Info", ("hostName", hostName)), cancellationToken);

    public Task<AxiomResponse<string>> GetWebsiteStateAsync(string hostName, string siteName, CancellationToken cancellationToken = default) =>
        GetAsync<string>(WithQuery("Manage/IIS/Website/State", ("hostName", hostName), ("siteName", siteName)), cancellationToken);

    public Task<AxiomResponse<bool>> StartWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/WebSite/Start", ("hostName", hostName), ("siteName", siteName)), null, cancellationToken);

    public Task<AxiomResponse<bool>> StopWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/Website/Stop", ("hostName", hostName), ("siteName", siteName)), null, cancellationToken);

    public Task<AxiomResponse<bool>> RestartWebsiteAsync(string hostName, string siteName, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Manage/IIS/Website/Restart", ("hostName", hostName), ("siteName", siteName)), null, cancellationToken);

    public Task<AxiomResponse<List<WindowsServiceInfo>>> GetServiceInfoAsync(string hostName, CancellationToken cancellationToken = default) =>
        GetAsync<List<WindowsServiceInfo>>(WithQuery("Manage/Service/Info", ("hostName", hostName)), cancellationToken);

    public Task<AxiomResponse<string>> GetServiceStateAsync(string hostName, string serviceName, CancellationToken cancellationToken = default) =>
        GetAsync<string>(WithQuery("Manage/Service/State", ("hostName", hostName), ("serviceName", serviceName)), cancellationToken);

    public Task<AxiomResponse<ServiceActionResult>> StartServiceAsync(string hostName, string serviceName, CancellationToken cancellationToken = default) =>
        PatchAsync<ServiceActionResult>(WithQuery("Manage/Service/Start", ("hostName", hostName), ("serviceName", serviceName)), null, cancellationToken);

    public Task<AxiomResponse<ServiceActionResult>> StopServiceAsync(string hostName, string serviceName, CancellationToken cancellationToken = default) =>
        PatchAsync<ServiceActionResult>(WithQuery("Manage/Service/Stop", ("hostName", hostName), ("serviceName", serviceName)), null, cancellationToken);
}
