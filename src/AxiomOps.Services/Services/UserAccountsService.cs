using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: UserAccounts.</summary>
public interface IUserAccountsService
{
    /// <summary>GET /UserAccounts</summary>
    Task<AxiomResponse<List<UserAccount>>> GetUserAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /UserAccounts/{loginName}</summary>
    Task<AxiomResponse<UserAccount>> GetUserAccountAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>GET /UserAccounts/RegisteredUsers</summary>
    Task<AxiomResponse<List<UserAccount>>> GetRegisteredUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /UserAccounts</summary>
    Task<AxiomResponse<bool>> CreateUserAccountAsync(CreateUserAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /UserAccounts/ExternalBalanceActions</summary>
    Task<AxiomResponse<List<ExternalBalanceAction>>> GetExternalBalanceActionsAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>GET /UserAccounts/GameEventData</summary>
    Task<AxiomResponse<List<GameEventData>>> GetGameEventDataAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /UserAccounts/Manage/Balance</summary>
    Task<AxiomResponse<bool>> SetUserBalanceAsync(SetUserBalanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /UserAccounts/Manage/Currency</summary>
    Task<AxiomResponse<bool>> SetUserCurrencyAsync(int userId, int currencyId, CancellationToken cancellationToken = default);

    /// <summary>GET /UserAccounts/Manage/MigrateToLVCS/{userId} — migrates the account to low-value currency support.</summary>
    Task<AxiomResponse<bool>> MigrateToLvcsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /UserAccounts/Manage/SessionReminder</summary>
    Task<AxiomResponse<int>> SetSessionReminderAsync(SetSessionReminderRequest request, CancellationToken cancellationToken = default);

    /// <summary>DELETE /UserAccounts/Manage/EndAllPlayerSessions</summary>
    Task<AxiomResponse<bool>> EndAllPlayerSessionsAsync(bool waitTilDone = false, CancellationToken cancellationToken = default);
}

public sealed class UserAccountsService(HttpClient http) : AxiomServiceBase(http), IUserAccountsService
{
    public Task<AxiomResponse<List<UserAccount>>> GetUserAccountsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<UserAccount>>("UserAccounts", cancellationToken);

    public Task<AxiomResponse<UserAccount>> GetUserAccountAsync(string loginName, CancellationToken cancellationToken = default) =>
        GetAsync<UserAccount>($"UserAccounts/{Uri.EscapeDataString(loginName)}", cancellationToken);

    public Task<AxiomResponse<List<UserAccount>>> GetRegisteredUsersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<UserAccount>>("UserAccounts/RegisteredUsers", cancellationToken);

    public Task<AxiomResponse<bool>> CreateUserAccountAsync(CreateUserAccountRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<bool>("UserAccounts", request, cancellationToken);

    public Task<AxiomResponse<List<ExternalBalanceAction>>> GetExternalBalanceActionsAsync(int productId, CancellationToken cancellationToken = default) =>
        GetAsync<List<ExternalBalanceAction>>(WithQuery("UserAccounts/ExternalBalanceActions", ("productId", productId)), cancellationToken);

    public Task<AxiomResponse<List<GameEventData>>> GetGameEventDataAsync(int userId, CancellationToken cancellationToken = default) =>
        GetAsync<List<GameEventData>>(WithQuery("UserAccounts/GameEventData", ("userId", userId)), cancellationToken);

    public Task<AxiomResponse<bool>> SetUserBalanceAsync(SetUserBalanceRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>("UserAccounts/Manage/Balance", request, cancellationToken);

    public Task<AxiomResponse<bool>> SetUserCurrencyAsync(int userId, int currencyId, CancellationToken cancellationToken = default) =>
        PatchAsync<bool>(WithQuery("UserAccounts/Manage/Currency", ("userId", userId), ("currencyId", currencyId)), null, cancellationToken);

    public Task<AxiomResponse<bool>> MigrateToLvcsAsync(int userId, CancellationToken cancellationToken = default) =>
        GetAsync<bool>($"UserAccounts/Manage/MigrateToLVCS/{userId}", cancellationToken);

    public Task<AxiomResponse<int>> SetSessionReminderAsync(SetSessionReminderRequest request, CancellationToken cancellationToken = default) =>
        PatchAsync<int>("UserAccounts/Manage/SessionReminder", request, cancellationToken);

    public Task<AxiomResponse<bool>> EndAllPlayerSessionsAsync(bool waitTilDone = false, CancellationToken cancellationToken = default) =>
        DeleteAsync<bool>(WithQuery("UserAccounts/Manage/EndAllPlayerSessions", ("waitTilDone", waitTilDone)), null, cancellationToken);
}
