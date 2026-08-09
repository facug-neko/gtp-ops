using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Launch.</summary>
public interface ILaunchService
{
    /// <summary>GET /Launch/GameUrl — returns the launch URL for a game.</summary>
    Task<AxiomResponse<string>> GetGameUrlAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /Launch/LobbyLinks</summary>
    Task<AxiomResponse<List<NameValue>>> GetLobbyLinksAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Launch/PlaycheckLinks</summary>
    Task<AxiomResponse<List<NameValue>>> GetPlaycheckLinksAsync(CancellationToken cancellationToken = default);
}

public sealed class LaunchService(HttpClient http) : AxiomServiceBase(http), ILaunchService
{
    public Task<AxiomResponse<string>> GetGameUrlAsync(GameLaunchRequest request, CancellationToken cancellationToken = default) =>
        GetAsync<string>(
            WithQuery("Launch/GameUrl",
                ("Username", request.Username),
                ("Password", request.Password),
                ("LobbyName", request.LobbyName),
                ("GameVersion", request.GameVersion),
                ("LanguageCode", request.LanguageCode),
                ("Host", request.Host),
                ("Iframe", request.Iframe),
                ("Framework", request.Framework),
                ("FrameworkVersion", request.FrameworkVersion),
                ("Site", request.Site),
                ("GameId", request.GameId),
                ("ModuleId", request.ModuleId),
                ("ClientId", request.ClientId)),
            cancellationToken);

    public Task<AxiomResponse<List<NameValue>>> GetLobbyLinksAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<NameValue>>("Launch/LobbyLinks", cancellationToken);

    public Task<AxiomResponse<List<NameValue>>> GetPlaycheckLinksAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<NameValue>>("Launch/PlaycheckLinks", cancellationToken);
}
