using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// User browser: lists every user of the environment with a live name filter,
/// and offers per-user actions on the selected row. First action: set balance
/// (PATCH /UserAccounts/Manage/Balance).
/// </summary>
public partial class UsersViewModel : ObservableObject
{
    private const string DefaultPassword = "test1234$";

    private readonly IUserAccountsService _userAccounts;
    private readonly IGamesService _games;
    private readonly IGameSettingsService _gameSettings;
    private readonly ILaunchService _launch;
    private readonly IMobileSettingsService _mobileSettings;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<UserAccount> _allUsers = [];
    private string? _latestTitanVersion;

    public ObservableCollection<UserAccount> Users { get; } = [];

    public ObservableCollection<InstalledGameRecord> Games { get; } = [];

    public ObservableCollection<string> LaunchGameVersions { get; } = [];

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBalanceCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseAllSessionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewPlaysCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private UserAccount? _selectedUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBalanceCommand))]
    private string _balanceAmount = string.Empty;

    [ObservableProperty]
    private string _serverId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseSessionCommand))]
    private InstalledGameRecord? _selectedSessionGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameIncognitoCommand))]
    private InstalledGameRecord? _selectedLaunchGame;

    [ObservableProperty]
    private string? _selectedLaunchGameVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBalanceCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseAllSessionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchGameIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _actionMessage;

    [ObservableProperty]
    private string? _actionError;

    public bool HasSelection => SelectedUser is not null;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public UsersViewModel(
        IUserAccountsService userAccounts,
        IGamesService games,
        IGameSettingsService gameSettings,
        ILaunchService launch,
        IMobileSettingsService mobileSettings,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _userAccounts = userAccounts;
        _games = games;
        _gameSettings = gameSettings;
        _launch = launch;
        _mobileSettings = mobileSettings;
        _context = context;
        _messenger = messenger;
    }

    partial void OnSelectedLaunchGameChanged(InstalledGameRecord? value)
    {
        LaunchGameVersions.Clear();
        foreach (var version in Services.VersionOrdering.Descending((value?.Versions ?? []).Select(v => v.Version)))
        {
            LaunchGameVersions.Add(version);
        }

        // Newest-first, so the latest build is preselected.
        SelectedLaunchGameVersion = LaunchGameVersions.FirstOrDefault();
    }

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    partial void OnSelectedUserChanged(UserAccount? value)
    {
        ActionMessage = null;
        ActionError = null;

        if (value is not null)
        {
            ServerId = value.ProductId.ToString(CultureInfo.InvariantCulture);
            BalanceAmount = string.Empty;
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await Task.WhenAll(LoadUsersAsync(keepSelection: false), LoadGamesAsync(), LoadTitanAsync());
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            var response = await _games.GetInstalledGameRecordsAsync();

            Games.Clear();
            foreach (var game in (response.DataObject ?? [])
                     .OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.ClientId))
            {
                Games.Add(game);
            }

            CloseAllSessionsCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            // Games only feed the session/launch cards; users can still browse without them.
            ActionError = $"No se pudieron cargar los juegos: {Shorten(ex.Message)}";
        }
    }

    private async Task LoadTitanAsync()
    {
        try
        {
            var response = await _mobileSettings.GetTitanVersionsAsync();
            _latestTitanVersion = Services.VersionOrdering.Latest((response.DataObject ?? []).Select(t => t.AppVersion));
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            // Launch falls back to letting the server pick if Titan couldn't be resolved.
            ActionError = $"No se pudo resolver la versión de Titan: {Shorten(ex.Message)}";
        }
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => LoadUsersAsync(keepSelection: true);

    private async Task LoadUsersAsync(bool keepSelection)
    {
        var selectedId = keepSelection ? SelectedUser?.UserId : null;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando usuarios…";

        try
        {
            var response = await _userAccounts.GetUserAccountsAsync();
            _allUsers = response.DataObject ?? [];
            ApplyFilter();

            if (selectedId is { } id)
            {
                SelectedUser = Users.FirstOrDefault(u => u.UserId == id);
            }

            StatusMessage = $"{_allUsers.Count} usuarios en {EnvironmentName}.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron cargar los usuarios: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanApplyBalance() =>
        !IsBusy
        && SelectedUser is not null
        && decimal.TryParse(BalanceAmount.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    [RelayCommand(CanExecute = nameof(CanApplyBalance))]
    private async Task ApplyBalanceAsync()
    {
        var user = SelectedUser!;
        var amount = decimal.Parse(BalanceAmount.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);

        if (!int.TryParse(ServerId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverId))
        {
            ActionError = $"ServerId inválido: \"{ServerId}\".";
            return;
        }

        IsBusy = true;
        ActionMessage = null;
        ActionError = null;

        try
        {
            var result = await _userAccounts.SetUserBalanceAsync(new SetUserBalanceRequest
            {
                UserId = user.UserId,
                ServerId = serverId,
                Amount = amount,
            });

            if (!result.Success)
            {
                ActionError = $"El servidor no devolvió success: {Shorten(result.CustomMessage)}";
                return;
            }

            await RefreshSelectedUserAsync(user);
            ActionMessage = $"Balance aplicado a {user.LoginName}: {amount.ToString(CultureInfo.InvariantCulture)} {SelectedUser?.CurrencyIsoCode ?? user.CurrencyIsoCode}.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ActionError = $"Error aplicando balance: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-fetches the user and swaps the row so the grid shows the fresh balance.</summary>
    private async Task RefreshSelectedUserAsync(UserAccount stale)
    {
        if (string.IsNullOrWhiteSpace(stale.LoginName))
        {
            return;
        }

        try
        {
            var response = await _userAccounts.GetUserAccountAsync(stale.LoginName);
            if (response.DataObject is not { } fresh)
            {
                return;
            }

            var allIndex = _allUsers.FindIndex(u => u.UserId == stale.UserId);
            if (allIndex >= 0)
            {
                _allUsers[allIndex] = fresh;
            }

            var viewIndex = Users.IndexOf(stale);
            if (viewIndex >= 0)
            {
                Users[viewIndex] = fresh;
                SelectedUser = fresh;
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            // The balance was applied; only the row refresh failed. Not fatal.
            ActionError = $"Balance aplicado, pero no se pudo refrescar la fila: {Shorten(ex.Message)}";
        }
    }

    private bool CanCloseSession() => !IsBusy && SelectedUser is not null && SelectedSessionGame is not null;

    /// <summary>DELETE /GameSettings/GameSession for the selected user + game.</summary>
    [RelayCommand(CanExecute = nameof(CanCloseSession))]
    private async Task CloseSessionAsync()
    {
        var user = SelectedUser!;
        var game = SelectedSessionGame!;

        IsBusy = true;
        ActionMessage = null;
        ActionError = null;

        try
        {
            var result = await _gameSettings.DeleteGameSessionsAsync(new DeleteGameSessionsRequest
            {
                UserId = user.UserId,
                ModuleId = game.ModuleId,
                ClientId = game.ClientId,
            });

            if (result.Success)
            {
                ActionMessage = $"Sesión de {user.LoginName} en {game.DisplayName} [{game.ModuleId}/{game.ClientId}] cerrada.";
            }
            else
            {
                ActionError = $"No se pudo cerrar la sesión: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ActionError = $"Error cerrando la sesión: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCloseAllSessions() => !IsBusy && SelectedUser is not null && Games.Count > 0;

    /// <summary>Closes the user's session on every installed game (one DELETE per game).</summary>
    [RelayCommand(CanExecute = nameof(CanCloseAllSessions))]
    private async Task CloseAllSessionsAsync()
    {
        var user = SelectedUser!;
        var games = Games.ToList();

        IsBusy = true;
        ActionMessage = null;
        ActionError = null;
        StatusMessage = $"Cerrando sesiones de {user.LoginName} en {games.Count} juegos…";

        var succeeded = 0;
        var failures = new List<string>();
        var gate = new SemaphoreSlim(6);

        try
        {
            await Task.WhenAll(games.Select(async game =>
            {
                await gate.WaitAsync();
                try
                {
                    var result = await _gameSettings.DeleteGameSessionsAsync(new DeleteGameSessionsRequest
                    {
                        UserId = user.UserId,
                        ModuleId = game.ModuleId,
                        ClientId = game.ClientId,
                    });

                    if (result.Success)
                    {
                        Interlocked.Increment(ref succeeded);
                    }
                    else
                    {
                        lock (failures)
                        {
                            failures.Add($"{game.DisplayName} [{game.ModuleId}/{game.ClientId}]: {Shorten(result.CustomMessage)}");
                        }
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    lock (failures)
                    {
                        failures.Add($"{game.DisplayName} [{game.ModuleId}/{game.ClientId}]: {Shorten(ex.Message)}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));

            ActionMessage = $"Sesiones de {user.LoginName}: {succeeded}/{games.Count} juegos OK.";
            if (failures.Count > 0)
            {
                ActionError = string.Join("\n", failures.Take(3));
                if (failures.Count > 3)
                {
                    ActionError += $"\n… y {failures.Count - 3} más.";
                }
            }
        }
        finally
        {
            StatusMessage = $"{_allUsers.Count} usuarios en {EnvironmentName}.";
            IsBusy = false;
        }
    }

    private bool CanLaunchGame() => !IsBusy && SelectedUser is not null && SelectedLaunchGame is not null;

    /// <summary>Quick launch for the selected user: fixed defaults (DotCom, en, desktop, latest Titan).</summary>
    [RelayCommand(CanExecute = nameof(CanLaunchGame))]
    private Task LaunchGameAsync() => LaunchCoreAsync(incognito: false);

    [RelayCommand(CanExecute = nameof(CanLaunchGame))]
    private Task LaunchGameIncognitoAsync() => LaunchCoreAsync(incognito: true);

    private async Task LaunchCoreAsync(bool incognito)
    {
        var user = SelectedUser!;
        var game = SelectedLaunchGame!;

        IsBusy = true;
        ActionMessage = null;
        ActionError = null;

        try
        {
            var response = await _launch.GetGameUrlAsync(new GameLaunchRequest
            {
                Username = user.LoginName,
                Password = DefaultPassword,
                LobbyName = "DotCom",
                ModuleId = game.ModuleId,
                ClientId = game.ClientId,
                GameVersion = SelectedLaunchGameVersion,
                LanguageCode = "en",
                Host = "desktop",
                Iframe = "false",
                Framework = "titan",
                FrameworkVersion = _latestTitanVersion,
            });

            if (!response.Success || string.IsNullOrWhiteSpace(response.DataObject))
            {
                ActionError = $"El server no devolvió la URL de launch: {Shorten(response.CustomMessage)}";
                return;
            }

            var browserError = AxiomOps.UI.Services.BrowserLauncher.Open(response.DataObject, incognito);
            if (browserError is not null)
            {
                ActionError = browserError;
                return;
            }

            ActionMessage = $"{game.DisplayName} lanzado con {user.LoginName}{(incognito ? " (incógnito)" : "")}.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ActionError = $"Error lanzando el juego: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanViewPlays() => SelectedUser is not null;

    [RelayCommand(CanExecute = nameof(CanViewPlays))]
    private void ViewPlays() =>
        _messenger.Send(new OpenGameEventDataMessage(SelectedUser!.UserId, SelectedUser.LoginName));

    [RelayCommand]
    private void CreateUser() => _messenger.Send(new OpenCreateUserMessage());

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private void ApplyFilter()
    {
        var filter = FilterText?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allUsers
            : _allUsers.Where(u =>
                (u.LoginName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (u.Username?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || u.UserId.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.Ordinal));

        Users.Clear();
        foreach (var user in filtered)
        {
            Users.Add(user);
        }

        if (!string.IsNullOrEmpty(filter))
        {
            StatusMessage = $"{Users.Count} de {_allUsers.Count} usuarios.";
        }
    }

    private static string Shorten(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "sin detalle";
        }

        var clean = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 220 ? clean : clean[..220] + "…";
    }
}
