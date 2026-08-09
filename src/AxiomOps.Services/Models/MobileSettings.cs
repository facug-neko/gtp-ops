using System.Text.Json.Serialization;

namespace AxiomOps.Services.Models;

public class Lobby
{
    [JsonPropertyName("lobbyID")]
    public int LobbyId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }

    [JsonPropertyName("registrationTypeID")]
    public int? RegistrationTypeId { get; set; }

    [JsonPropertyName("registrationCasinoID")]
    public int? RegistrationCasinoId { get; set; }

    [JsonPropertyName("bingoCasinoID")]
    public int? BingoCasinoId { get; set; }

    [JsonPropertyName("casinoCasinoID")]
    public int? CasinoCasinoId { get; set; }

    [JsonPropertyName("pokerCasinoID")]
    public int? PokerCasinoId { get; set; }

    [JsonPropertyName("registrationEndPointID")]
    public int? RegistrationEndPointId { get; set; }

    [JsonPropertyName("bankingEndPointID")]
    public int? BankingEndPointId { get; set; }

    [JsonPropertyName("promotionsEndPointID")]
    public int? PromotionsEndPointId { get; set; }

    [JsonPropertyName("operatorTrackingEndPointID")]
    public int? OperatorTrackingEndPointId { get; set; }

    public string? StringName { get; set; }

    [JsonPropertyName("lobbyEndPointID")]
    public int? LobbyEndPointId { get; set; }

    [JsonPropertyName("casinoPracticeCasinoID")]
    public int? CasinoPracticeCasinoId { get; set; }

    [JsonPropertyName("changePasswordEndPointID")]
    public int? ChangePasswordEndPointId { get; set; }

    [JsonPropertyName("loginEndPointID")]
    public int? LoginEndPointId { get; set; }

    [JsonPropertyName("responsibleGamingEndPointID")]
    public int? ResponsibleGamingEndPointId { get; set; }

    [JsonPropertyName("currencyDisplayFormatOverrideID")]
    public int? CurrencyDisplayFormatOverrideId { get; set; }

    [JsonPropertyName("helpEndPointID")]
    public int? HelpEndPointId { get; set; }

    [JsonPropertyName("mobileWebServicesEndPointID")]
    public int? MobileWebServicesEndPointId { get; set; }

    [JsonPropertyName("lobbyTypeID")]
    public int? LobbyTypeId { get; set; }

    public int? MigrationEndPointId { get; set; }
    public int? ActivityStatementEndPointId { get; set; }
    public string? FriendlyName { get; set; }
}

public class TitanVersion
{
    public string? AppVersion { get; set; }
    public string? FileVersion { get; set; }
    public string? DefaultConfigVersion { get; set; }
    public string? HostingEnvironment { get; set; }
    public bool? IsHealthy { get; set; }
}
