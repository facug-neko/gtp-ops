namespace AxiomOps.Services.Models;

/// <summary>Query parameters for building a game launch URL. Null values are omitted.</summary>
public class GameLaunchRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? LobbyName { get; set; }
    public string? GameVersion { get; set; }
    public string? LanguageCode { get; set; }
    public string? Host { get; set; }
    public string? Iframe { get; set; }
    public string? Framework { get; set; }
    public string? FrameworkVersion { get; set; }
    public string? Site { get; set; }
    public string? GameId { get; set; }
    public int? ModuleId { get; set; }
    public int? ClientId { get; set; }
}
