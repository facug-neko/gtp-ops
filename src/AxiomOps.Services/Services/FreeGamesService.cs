using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: FreeGames.</summary>
public interface IFreeGamesService
{
    /// <summary>POST /FreeGames/FreeGamesOffer</summary>
    Task<AxiomResponse<bool>> CreateFreeGamesOfferAsync(CreateFreeGamesOfferRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /FreeGames/FreeGamesOptions</summary>
    Task<AxiomResponse<FreeGamesOptions>> GetFreeGamesOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class FreeGamesService(HttpClient http) : AxiomServiceBase(http), IFreeGamesService
{
    public Task<AxiomResponse<bool>> CreateFreeGamesOfferAsync(CreateFreeGamesOfferRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("FreeGames/FreeGamesOffer", request, cancellationToken);

    public Task<AxiomResponse<FreeGamesOptions>> GetFreeGamesOptionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<FreeGamesOptions>("FreeGames/FreeGamesOptions", cancellationToken);
}
