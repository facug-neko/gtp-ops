using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: MobileSettings.</summary>
public interface IMobileSettingsService
{
    /// <summary>GET /MobileSettings/Lobbies</summary>
    Task<AxiomResponse<List<Lobby>>> GetLobbiesAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /MobileSettings/LobbyGameFrameworkUrl</summary>
    Task<AxiomResponse<bool>> SetLobbyGameFrameworkUrlAsync(string framework, string version, CancellationToken cancellationToken = default);

    /// <summary>GET /MobileSettings/TitanVersions</summary>
    Task<AxiomResponse<List<TitanVersion>>> GetTitanVersionsAsync(CancellationToken cancellationToken = default);
}

public sealed class MobileSettingsService(HttpClient http) : AxiomServiceBase(http), IMobileSettingsService
{
    public Task<AxiomResponse<List<Lobby>>> GetLobbiesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<Lobby>>("MobileSettings/Lobbies", cancellationToken);

    public Task<AxiomResponse<bool>> SetLobbyGameFrameworkUrlAsync(string framework, string version, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("MobileSettings/LobbyGameFrameworkUrl", ("framework", framework), ("version", version)), null, cancellationToken);

    public Task<AxiomResponse<List<TitanVersion>>> GetTitanVersionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<TitanVersion>>("MobileSettings/TitanVersions", cancellationToken);
}
