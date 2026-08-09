using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Authorization.</summary>
public interface IAuthorizationService
{
    /// <summary>POST /Authorization/ApiKeys/Create</summary>
    Task<AxiomResponse<bool>> CreateApiKeyAsync(ApiKey request, CancellationToken cancellationToken = default);

    /// <summary>DELETE /Authorization/ApiKeys/Revoke</summary>
    Task<AxiomResponse<bool>> RevokeApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>GET /Authorization/ApiKeys</summary>
    Task<AxiomResponse<List<ApiKey>>> GetApiKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Authorization/GlobalSecurityBypass</summary>
    Task<AxiomResponse<bool>> GetGlobalSecurityBypassAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /Authorization/GlobalSecurityBypass</summary>
    Task<AxiomResponse<bool>> SetGlobalSecurityBypassAsync(bool globalSecurityBypass, CancellationToken cancellationToken = default);
}

public sealed class AuthorizationService(HttpClient http) : AxiomServiceBase(http), IAuthorizationService
{
    public Task<AxiomResponse<bool>> CreateApiKeyAsync(ApiKey request, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("Authorization/ApiKeys/Create", request, cancellationToken);

    public Task<AxiomResponse<bool>> RevokeApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) =>
        DeleteAsync<bool>("Authorization/ApiKeys/Revoke", apiKey, cancellationToken);

    public Task<AxiomResponse<List<ApiKey>>> GetApiKeysAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<ApiKey>>("Authorization/ApiKeys", cancellationToken);

    public Task<AxiomResponse<bool>> GetGlobalSecurityBypassAsync(CancellationToken cancellationToken = default) =>
        GetAsync<bool>("Authorization/GlobalSecurityBypass", cancellationToken);

    public Task<AxiomResponse<bool>> SetGlobalSecurityBypassAsync(bool globalSecurityBypass, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Authorization/GlobalSecurityBypass", ("globalSecurityBypass", globalSecurityBypass)), null, cancellationToken);
}
