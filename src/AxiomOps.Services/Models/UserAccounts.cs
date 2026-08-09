namespace AxiomOps.Services.Models;

public class UserAccount
{
    public int UserId { get; set; }
    public string? LoginName { get; set; }
    public string? Username { get; set; }
    public int LoginStatus { get; set; }
    public int LoginAttempts { get; set; }
    public int LockoutStatus { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? RegulatedMarket { get; set; }
    public string? RegulatedMarketIsoCode { get; set; }

    /// <summary>Balance in CENTS (1_009_340 = 10,093.40 in currency units). Note: PATCH Manage/Balance takes units, not cents.</summary>
    public decimal Balance { get; set; }
    public int CurrencyId { get; set; }
    public string? CurrencyIsoCode { get; set; }
    public bool IsQuickfire { get; set; }
}

public class ExternalBalanceAction
{
    public long ActionId { get; set; }
    public long LogId { get; set; }
    public int ProductId { get; set; }
    public long Amount { get; set; }
    public long CashBalance { get; set; }
    public long BonusBalance { get; set; }
    public string? ExternalReference { get; set; }
    public string? IsoCurrencyCode { get; set; }
    public DateTimeOffset? TransactionTime { get; set; }
}

public class GameEventData
{
    public int TransactionNumber { get; set; }
    public int EventNumber { get; set; }
    public int ModuleId { get; set; }
    public string? EventDataByteStream { get; set; }
    public string? GameDataByteStream { get; set; }
    public string? StatsDataByteStream { get; set; }
    public DateTimeOffset? TransactionTime { get; set; }
}

public class SetUserBalanceRequest
{
    public int UserId { get; set; }
    public int ServerId { get; set; }
    public decimal Amount { get; set; }
}

public class SetSessionReminderRequest
{
    public int UserId { get; set; }
    public int PeriodType { get; set; }
    public int PeriodValue { get; set; }
}

public class CreateUserAccountRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int MarketTypeId { get; set; }
    public int ServerId { get; set; }
    public int UserTypeId { get; set; }
    public string? CurrencyIsoCode { get; set; }
    public string? Country { get; set; }
    public int NumberOfAccounts { get; set; } = 1;
}
