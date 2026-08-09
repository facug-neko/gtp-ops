using System.Text.Json.Serialization;

namespace AxiomOps.Services.Models;

public class GameProvider
{
    public int GameProviderId { get; set; }
    public string? GameProviderName { get; set; }
}

public class DeleteGameSessionsRequest
{
    public int ModuleId { get; set; }
    public int ClientId { get; set; }
    public int UserId { get; set; }
}

/// <summary>Body shared by the mobile/filter-map database setting endpoints.</summary>
public class GameDatabaseSettingsRequest
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public string? FilterType { get; set; }
    public string? GameProvider { get; set; }
    public string? GameCategory { get; set; }
}

public class CasinoDatabaseSettingsRequest
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public string? FilterType { get; set; }
}

public class ForceGameSettingsRequest
{
    public int ClientId { get; set; }

    // The API contract exposes both "clientId" and "clientID"; kept verbatim.
    [JsonPropertyName("clientID")]
    public int ClientIdUpper { get; set; }

    public string? LoginName { get; set; }
    public int ModuleId { get; set; }
    public string? State { get; set; }
}

public class SummarizedBonusRoundSettingRequest
{
    public int ProductId { get; set; }
    public int IntValue { get; set; }
}

public class GamePresetConfigurationRequest
{
    [JsonPropertyName("is64bit")]
    public bool Is64Bit { get; set; }
}
