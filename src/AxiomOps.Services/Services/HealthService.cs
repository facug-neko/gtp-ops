using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Health.</summary>
public interface IHealthService
{
    /// <summary>GET /Health — full appliance state (metadata, health and per-host statistics).</summary>
    Task<AxiomResponse<ApplianceState>> GetApplianceStateAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Health/ApplianceHealth</summary>
    Task<AxiomResponse<List<HostHealthReport>>> GetApplianceHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Health/ApplianceHostEntries</summary>
    Task<AxiomResponse<ApplianceHostEntries>> GetApplianceHostEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Health/HostHealth</summary>
    Task<AxiomResponse<HostHealth>> GetHostHealthAsync(string hostIdentification, CancellationToken cancellationToken = default);

    /// <summary>GET /Health/ValidateHostEntry</summary>
    Task<AxiomResponse> ValidateHostEntryAsync(string hostIdentification, CancellationToken cancellationToken = default);
}

public sealed class HealthService(HttpClient http) : AxiomServiceBase(http), IHealthService
{
    public Task<AxiomResponse<ApplianceState>> GetApplianceStateAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ApplianceState>("Health", cancellationToken);

    public Task<AxiomResponse<List<HostHealthReport>>> GetApplianceHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<HostHealthReport>>("Health/ApplianceHealth", cancellationToken);

    public Task<AxiomResponse<ApplianceHostEntries>> GetApplianceHostEntriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ApplianceHostEntries>("Health/ApplianceHostEntries", cancellationToken);

    public Task<AxiomResponse<HostHealth>> GetHostHealthAsync(string hostIdentification, CancellationToken cancellationToken = default) =>
        GetAsync<HostHealth>(WithQuery("Health/HostHealth", ("hostIdentification", hostIdentification)), cancellationToken);

    public async Task<AxiomResponse> ValidateHostEntryAsync(string hostIdentification, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<System.Text.Json.JsonElement?>(
            WithQuery("Health/ValidateHostEntry", ("hostIdentification", hostIdentification)),
            cancellationToken).ConfigureAwait(false);

        return new AxiomResponse
        {
            Success = response.Success,
            CustomMessage = response.CustomMessage,
            DataObject = response.DataObject,
            ResultSets = response.ResultSets,
        };
    }
}
