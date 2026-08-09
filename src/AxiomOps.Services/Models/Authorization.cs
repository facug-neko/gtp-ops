namespace AxiomOps.Services.Models;

/// <summary>API key record; also used as the payload when creating one.</summary>
public class ApiKey
{
    public DateTimeOffset? Expiration { get; set; }
    public string? Key { get; set; }
    public string? User { get; set; }
}
