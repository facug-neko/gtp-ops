namespace AxiomOps.Services.Models;

public class FreeGamesOptions
{
    public List<FreeGameSupportedGame>? FreeGameSupportedGames { get; set; }
    public List<DurationSetting>? DurationSettings { get; set; }
}

public class FreeGameSupportedGame : GameRecord
{
    public ValidBetLimits? ValidBetLimits { get; set; }
}

public class ValidBetLimits
{
    public int ModuleId { get; set; }
    public int? Coins { get; set; }
    public int? Paylines { get; set; }
    public int? BetMultiplier { get; set; }
    public string? ChipSizes { get; set; }
    public List<string>? CoinList { get; set; }
    public List<string>? ChipSizeList { get; set; }
}

public class CreateFreeGamesOfferRequest
{
    public FreeGameOfferByNearestCostRequest? FreeGameOfferByNearestCostRequest { get; set; }
    public int UserId { get; set; }
}

public class FreeGameOfferByNearestCostRequest
{
    public int? DefaultNumberOfGames { get; set; }
    public List<FreeGameOfferGame>? Games { get; set; }
    public Guid IdempotencyId { get; set; }
    public bool Reuse { get; set; }
    public int BalanceTypeId { get; set; }
    public string? DefaultDisplayLine1 { get; set; }
    public string? DefaultDisplayLine2 { get; set; }
    public OfferDuration? DurationAvailableAfterAwarded { get; set; }
    public DateTimeOffset? OfferAvailableFromUtcDate { get; set; }
    public DateTimeOffset? OfferExpirationUtcDate { get; set; }
    public string? OfferName { get; set; }
}

public class FreeGameOfferGame
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public double? NearestCostPerBet { get; set; }
    public string? GameName { get; set; }
    public long? NumberOfCoins { get; set; }
}

public class OfferDuration
{
    public int Length { get; set; }
    public int TimeUnitId { get; set; }
}
