using System.Text.Json.Serialization;

namespace AxiomOps.Services.Models;

public class ProgressiveBet
{
    public int UserId { get; set; }
    public int UserTransNumber { get; set; }
    public long WagerAmountInCents { get; set; }
    public double ContributionAmountInDecimalCents { get; set; }
    public DateTimeOffset? Time { get; set; }
}

public class ProgressiveSetting
{
    public int ProgressiveId { get; set; }
    public long BaseIncrementRate { get; set; }
    public long ResetValue { get; set; }
    public long JackpotValue { get; set; }
    public long LoanBalance { get; set; }
    public int JackpotNumber { get; set; }
    public string? ProgressiveName { get; set; }
}

public class ProgressiveSettingUpdate
{
    public int ProgressiveId { get; set; }
    public long BaseIncrementRate { get; set; }
    public long ResetValue { get; set; }
    public long JackpotValue { get; set; }
    public long LoanBalance { get; set; }
}

public class WinCashInConfig
{
    public long BalanceResetAmount { get; set; }
    public long MaxBalanceThreshold { get; set; }

    [JsonPropertyName("maxJITCashinAmount")]
    public long MaxJitCashInAmount { get; set; }
}

public class WinValidationRoutine
{
    public string? DisplayName { get; set; }
    public DateTimeOffset? DateAdded { get; set; }
    public string? ObjectHash { get; set; }
}

public class ProgressiveWinValidation
{
    public string? ModuleName { get; set; }
    public string? ValidationMessage { get; set; }
    public int UserId { get; set; }
    public string? LoginName { get; set; }
    public DateTimeOffset? ProgressiveWinTime { get; set; }
    public double JackpotValue { get; set; }
    public int UserTransNumber { get; set; }
    public int TransNumber { get; set; }
    public string? Currency { get; set; }
    public string? CasinoName { get; set; }
    public string? ProgressiveType { get; set; }
    public string? FirstName { get; set; }
    public string? Surname { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
    public int TransactionNumber { get; set; }
}

public class WinValidationBehavior
{
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("erR_Message")]
    public string? ErrMessage { get; set; }

    public string? Message { get; set; }
    public string? ValidationMessage { get; set; }
    public int UserId { get; set; }
    public string? LoginName { get; set; }
    public DateTimeOffset? Date { get; set; }
    public string? StoreProc { get; set; }
}

public class ProgressiveWin
{
    public int ModuleId { get; set; }
    public int GamePayId { get; set; }
    public int JackpotNumber { get; set; }
    public int ServerId { get; set; }
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public long CoinSize { get; set; }
    public long PlayerCurrencyJackpotWinAmount { get; set; }
    public long BaseCurrencyJackpotWinAmount { get; set; }
    public DateTimeOffset? ProgressiveWinTime { get; set; }
    public long PlayerCurrencyCreditsJackpotWinAmount { get; set; }
    public long JackpotResetAmount { get; set; }
    public long ReserveChangeAmount { get; set; }
    public long FinalReserve { get; set; }
    public long LoanBalanceChangeAmount { get; set; }
    public long FinalLoanBalance { get; set; }
}

public class BaseIncrementRateUpdate
{
    public int ProgressiveId { get; set; }
    public long BaseIncrementRate { get; set; }
}

public class JackpotValueUpdate
{
    public int ProgressiveId { get; set; }
    public long JackpotValue { get; set; }
}

public class LoanBalanceUpdate
{
    public int ProgressiveId { get; set; }
    public long LoanBalance { get; set; }
}

public class ResetValueUpdate
{
    public int ProgressiveId { get; set; }
    public long ResetValue { get; set; }
}
