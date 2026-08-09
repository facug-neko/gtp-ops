using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: BetSettings.</summary>
public interface IBetSettingsService
{
    /// <summary>GET /BetSettings/MultiplierTemplateSettings</summary>
    Task<AxiomResponse<List<MultiplierTemplateSetting>>> GetMultiplierTemplateSettingsAsync(int moduleId, int clientId, CancellationToken cancellationToken = default);

    /// <summary>GET /BetSettings/UserGameBetSettings</summary>
    Task<AxiomResponse<UserGameBetSettings>> GetUserGameBetSettingsAsync(int userId, int moduleId, int clientId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /BetSettings/UserGameBetSettings</summary>
    Task<AxiomResponse<bool>> SetUserGameBetSettingsAsync(SetUserGameBetSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /BetSettings/ValidateUserBetSettings</summary>
    Task<AxiomResponse<bool>> ValidateUserBetSettingsAsync(UserGameBetSettings request, CancellationToken cancellationToken = default);
}

public sealed class BetSettingsService(HttpClient http) : AxiomServiceBase(http), IBetSettingsService
{
    public Task<AxiomResponse<List<MultiplierTemplateSetting>>> GetMultiplierTemplateSettingsAsync(int moduleId, int clientId, CancellationToken cancellationToken = default) =>
        GetAsync<List<MultiplierTemplateSetting>>(
            WithQuery("BetSettings/MultiplierTemplateSettings", ("moduleId", moduleId), ("clientId", clientId)),
            cancellationToken);

    public Task<AxiomResponse<UserGameBetSettings>> GetUserGameBetSettingsAsync(int userId, int moduleId, int clientId, CancellationToken cancellationToken = default) =>
        GetAsync<UserGameBetSettings>(
            WithQuery("BetSettings/UserGameBetSettings", ("UserId", userId), ("ModuleId", moduleId), ("ClientId", clientId)),
            cancellationToken);

    public Task<AxiomResponse<bool>> SetUserGameBetSettingsAsync(SetUserGameBetSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("BetSettings/UserGameBetSettings", request, cancellationToken);

    public Task<AxiomResponse<bool>> ValidateUserBetSettingsAsync(UserGameBetSettings request, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("BetSettings/ValidateUserBetSettings", request, cancellationToken);
}
