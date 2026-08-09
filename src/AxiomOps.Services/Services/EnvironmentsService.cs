using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Environments.</summary>
public interface IEnvironmentsService
{
    /// <summary>GET /Environments/LastProvision</summary>
    Task<AxiomResponse<DateTimeOffset?>> GetLastProvisionDateAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /Environments/SoftwareProducts</summary>
    Task<AxiomResponse<List<HostSoftwareProducts>>> GetSoftwareProductsAsync(CancellationToken cancellationToken = default);
}

public sealed class EnvironmentsService(HttpClient http) : AxiomServiceBase(http), IEnvironmentsService
{
    public Task<AxiomResponse<DateTimeOffset?>> GetLastProvisionDateAsync(CancellationToken cancellationToken = default) =>
        GetAsync<DateTimeOffset?>("Environments/LastProvision", cancellationToken);

    public Task<AxiomResponse<List<HostSoftwareProducts>>> GetSoftwareProductsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<HostSoftwareProducts>>("Environments/SoftwareProducts", cancellationToken);
}
