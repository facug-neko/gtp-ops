using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: Progressives.</summary>
public interface IProgressivesService
{
    /// <summary>GET /Progressives/BetLog</summary>
    Task<AxiomResponse<List<ProgressiveBet>>> GetBetLogAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/Settings</summary>
    Task<AxiomResponse<List<ProgressiveSetting>>> GetSettingsAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/Settings</summary>
    Task<AxiomResponse<bool>> SetSettingsAsync(IReadOnlyCollection<ProgressiveSettingUpdate> settings, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/WinCashInConfig</summary>
    Task<AxiomResponse<WinCashInConfig>> GetWinCashInConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/WinCashInConfig</summary>
    Task<AxiomResponse<bool>> SetWinCashInConfigAsync(WinCashInConfig config, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/WinValidation</summary>
    Task<AxiomResponse<List<WinValidationRoutine>>> GetWinValidationRoutinesAsync(string routineName, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/ValidateProgressiveWin</summary>
    Task<AxiomResponse<List<List<ProgressiveWinValidation>>>> ValidateProgressiveWinAsync(int moduleId, string routineName, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/WinValidationBehavior</summary>
    Task<AxiomResponse<List<WinValidationBehavior>>> GetWinValidationBehaviorAsync(int moduleId, string routineName, int userId, int gamePayId, string date, CancellationToken cancellationToken = default);

    /// <summary>GET /Progressives/Wins</summary>
    Task<AxiomResponse<List<ProgressiveWin>>> GetWinsAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/BaseIncrementRates</summary>
    Task<AxiomResponse<bool>> SetBaseIncrementRatesAsync(IReadOnlyCollection<BaseIncrementRateUpdate> values, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/JackpotValues</summary>
    Task<AxiomResponse<bool>> SetJackpotValuesAsync(IReadOnlyCollection<JackpotValueUpdate> values, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/LoanBalances</summary>
    Task<AxiomResponse<bool>> SetLoanBalancesAsync(IReadOnlyCollection<LoanBalanceUpdate> values, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/ResetValues</summary>
    Task<AxiomResponse<bool>> SetResetValuesAsync(IReadOnlyCollection<ResetValueUpdate> values, CancellationToken cancellationToken = default);

    /// <summary>PATCH /Progressives/ScheduledLoanBalanceReset</summary>
    Task<AxiomResponse<bool>> SetScheduledLoanBalanceResetAsync(bool isEnabled, CancellationToken cancellationToken = default);
}

public sealed class ProgressivesService(HttpClient http) : AxiomServiceBase(http), IProgressivesService
{
    public Task<AxiomResponse<List<ProgressiveBet>>> GetBetLogAsync(int moduleId, CancellationToken cancellationToken = default) =>
        GetAsync<List<ProgressiveBet>>(WithQuery("Progressives/BetLog", ("moduleId", moduleId)), cancellationToken);

    public Task<AxiomResponse<List<ProgressiveSetting>>> GetSettingsAsync(int moduleId, CancellationToken cancellationToken = default) =>
        GetAsync<List<ProgressiveSetting>>(WithQuery("Progressives/Settings", ("moduleId", moduleId)), cancellationToken);

    public Task<AxiomResponse<bool>> SetSettingsAsync(IReadOnlyCollection<ProgressiveSettingUpdate> settings, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/Settings", settings, cancellationToken);

    public Task<AxiomResponse<WinCashInConfig>> GetWinCashInConfigAsync(CancellationToken cancellationToken = default) =>
        GetAsync<WinCashInConfig>("Progressives/WinCashInConfig", cancellationToken);

    public Task<AxiomResponse<bool>> SetWinCashInConfigAsync(WinCashInConfig config, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/WinCashInConfig", config, cancellationToken);

    public Task<AxiomResponse<List<WinValidationRoutine>>> GetWinValidationRoutinesAsync(string routineName, CancellationToken cancellationToken = default) =>
        GetAsync<List<WinValidationRoutine>>(WithQuery("Progressives/WinValidation", ("routineName", routineName)), cancellationToken);

    public Task<AxiomResponse<List<List<ProgressiveWinValidation>>>> ValidateProgressiveWinAsync(int moduleId, string routineName, CancellationToken cancellationToken = default) =>
        GetAsync<List<List<ProgressiveWinValidation>>>(
            WithQuery("Progressives/ValidateProgressiveWin", ("moduleId", moduleId), ("routineName", routineName)),
            cancellationToken);

    public Task<AxiomResponse<List<WinValidationBehavior>>> GetWinValidationBehaviorAsync(int moduleId, string routineName, int userId, int gamePayId, string date, CancellationToken cancellationToken = default) =>
        GetAsync<List<WinValidationBehavior>>(
            WithQuery("Progressives/WinValidationBehavior",
                ("moduleId", moduleId),
                ("routineName", routineName),
                ("userId", userId),
                ("gamePayId", gamePayId),
                ("date", date)),
            cancellationToken);

    public Task<AxiomResponse<List<ProgressiveWin>>> GetWinsAsync(int moduleId, CancellationToken cancellationToken = default) =>
        GetAsync<List<ProgressiveWin>>(WithQuery("Progressives/Wins", ("moduleId", moduleId)), cancellationToken);

    public Task<AxiomResponse<bool>> SetBaseIncrementRatesAsync(IReadOnlyCollection<BaseIncrementRateUpdate> values, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/BaseIncrementRates", values, cancellationToken);

    public Task<AxiomResponse<bool>> SetJackpotValuesAsync(IReadOnlyCollection<JackpotValueUpdate> values, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/JackpotValues", values, cancellationToken);

    public Task<AxiomResponse<bool>> SetLoanBalancesAsync(IReadOnlyCollection<LoanBalanceUpdate> values, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/LoanBalances", values, cancellationToken);

    public Task<AxiomResponse<bool>> SetResetValuesAsync(IReadOnlyCollection<ResetValueUpdate> values, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("Progressives/ResetValues", values, cancellationToken);

    public Task<AxiomResponse<bool>> SetScheduledLoanBalanceResetAsync(bool isEnabled, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("Progressives/ScheduledLoanBalanceReset", ("isEnabled", isEnabled)), null, cancellationToken);
}
