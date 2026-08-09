using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Games.</summary>
public interface IGamesService
{
    /// <summary>GET /Games/DependencyCheck</summary>
    Task<AxiomResponse<List<GameDependencyReport>>> GetGameDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Games/InstalledDatabaseGameRecords</summary>
    Task<AxiomResponse<List<GameRecord>>> GetInstalledDatabaseGameRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Games/InstalledGameRecords</summary>
    Task<AxiomResponse<List<InstalledGameRecord>>> GetInstalledGameRecordsAsync(CancellationToken cancellationToken = default);
}

public sealed class GamesService(HttpClient http) : AxiomServiceBase(http), IGamesService
{
    public Task<AxiomResponse<List<GameDependencyReport>>> GetGameDependenciesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<GameDependencyReport>>("Games/DependencyCheck", cancellationToken);

    public Task<AxiomResponse<List<GameRecord>>> GetInstalledDatabaseGameRecordsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<GameRecord>>("Games/InstalledDatabaseGameRecords", cancellationToken);

    public Task<AxiomResponse<List<InstalledGameRecord>>> GetInstalledGameRecordsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<InstalledGameRecord>>("Games/InstalledGameRecords", cancellationToken);
}
