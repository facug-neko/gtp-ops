using System.Text.Json;
using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: GameSettings.</summary>
public interface IGameSettingsService
{
    /// <summary>GET /GameSettings/Viper/RegistryKey</summary>
    Task<AxiomResponse<JsonElement?>> GetViperRegistryKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /GameSettings/GameProvider</summary>
    Task<AxiomResponse<bool>> CreateGameProviderAsync(string gameProvider, CancellationToken cancellationToken = default);

    /// <summary>GET /GameSettings/GameProviders</summary>
    Task<AxiomResponse<List<GameProvider>>> GetGameProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>DELETE /GameSettings/GameSession</summary>
    Task<AxiomResponse<bool>> DeleteGameSessionsAsync(DeleteGameSessionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /GameSettings/FilterMaps</summary>
    Task<AxiomResponse<List<string>>> GetFilterMapsAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/CustomMobileDatabaseSettings</summary>
    Task<AxiomResponse<bool>> SetCustomMobileDatabaseSettingsAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/CustomCasinoDatabaseSettings</summary>
    Task<AxiomResponse<bool>> SetCustomCasinoDatabaseSettingsAsync(CasinoDatabaseSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/ForceGameSettings</summary>
    Task<AxiomResponse<bool>> SetForceGameSettingsAsync(ForceGameSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /GameSettings/SpecificGamePresetConfiguration — true when the game preset is 64-bit.</summary>
    Task<AxiomResponse<bool>> GetSpecificGamePresetConfigurationAsync(int moduleId, int clientId, CancellationToken cancellationToken = default);

    /// <summary>GET /GameSettings/GamePresetConfiguration — true when the preset configuration is 64-bit.</summary>
    Task<AxiomResponse<bool>> GetGamePresetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/GamePresetConfiguration</summary>
    Task<AxiomResponse<bool>> SetGamePresetConfigurationAsync(bool is64Bit, CancellationToken cancellationToken = default);

    /// <summary>GET /GameSettings/SummarizedBonusRoundSetting</summary>
    Task<AxiomResponse<bool>> GetSummarizedBonusRoundSettingAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/SummarizedBonusRoundSetting</summary>
    Task<AxiomResponse<bool>> SetSummarizedBonusRoundSettingAsync(SummarizedBonusRoundSettingRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/MobileGameDatabaseSettings</summary>
    Task<AxiomResponse<bool>> SetMobileGameDatabaseSettingsAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /GameSettings/FilterMapType</summary>
    Task<AxiomResponse<bool>> SetFilterMapTypeAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default);
}

public sealed class GameSettingsService(HttpClient http) : AxiomServiceBase(http), IGameSettingsService
{
    public Task<AxiomResponse<JsonElement?>> GetViperRegistryKeyAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement?>("GameSettings/Viper/RegistryKey", cancellationToken);

    public Task<AxiomResponse<bool>> CreateGameProviderAsync(string gameProvider, CancellationToken cancellationToken = default) =>
        PostAsync<bool>(WithQuery("GameSettings/GameProvider", ("gameProvider", gameProvider)), null, cancellationToken);

    public Task<AxiomResponse<List<GameProvider>>> GetGameProvidersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<GameProvider>>("GameSettings/GameProviders", cancellationToken);

    public Task<AxiomResponse<bool>> DeleteGameSessionsAsync(DeleteGameSessionsRequest request, CancellationToken cancellationToken = default) =>
        DeleteAsync<bool>("GameSettings/GameSession", request, cancellationToken);

    public Task<AxiomResponse<List<string>>> GetFilterMapsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<string>>("GameSettings/FilterMaps", cancellationToken);

    public Task<AxiomResponse<bool>> SetCustomMobileDatabaseSettingsAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/CustomMobileDatabaseSettings", request, cancellationToken);

    public Task<AxiomResponse<bool>> SetCustomCasinoDatabaseSettingsAsync(CasinoDatabaseSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/CustomCasinoDatabaseSettings", request, cancellationToken);

    public Task<AxiomResponse<bool>> SetForceGameSettingsAsync(ForceGameSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/ForceGameSettings", request, cancellationToken);

    public Task<AxiomResponse<bool>> GetSpecificGamePresetConfigurationAsync(int moduleId, int clientId, CancellationToken cancellationToken = default) =>
        GetAsync<bool>(WithQuery("GameSettings/SpecificGamePresetConfiguration", ("moduleId", moduleId), ("clientId", clientId)), cancellationToken);

    public Task<AxiomResponse<bool>> GetGamePresetConfigurationAsync(CancellationToken cancellationToken = default) =>
        GetAsync<bool>("GameSettings/GamePresetConfiguration", cancellationToken);

    public Task<AxiomResponse<bool>> SetGamePresetConfigurationAsync(bool is64Bit, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/GamePresetConfiguration", new GamePresetConfigurationRequest { Is64Bit = is64Bit }, cancellationToken);

    public Task<AxiomResponse<bool>> GetSummarizedBonusRoundSettingAsync(CancellationToken cancellationToken = default) =>
        GetAsync<bool>("GameSettings/SummarizedBonusRoundSetting", cancellationToken);

    public Task<AxiomResponse<bool>> SetSummarizedBonusRoundSettingAsync(SummarizedBonusRoundSettingRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/SummarizedBonusRoundSetting", request, cancellationToken);

    public Task<AxiomResponse<bool>> SetMobileGameDatabaseSettingsAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/MobileGameDatabaseSettings", request, cancellationToken);

    public Task<AxiomResponse<bool>> SetFilterMapTypeAsync(GameDatabaseSettingsRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("GameSettings/FilterMapType", request, cancellationToken);
}
