using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// Launch module: pick user + game + market (lobby), build the launch URL via
/// GET /Launch/GameUrl and open it in a normal or incognito browser window.
/// Parameter recipe mirrors the legacy axiom-admin tool: Host=desktop,
/// Iframe=false, Framework=titan, GameVersion=latest, LobbyName=friendly name.
/// </summary>
public partial class LaunchViewModel : ObservableObject
{
    private const string DefaultPassword = "test1234$";

    private readonly ILaunchService _launch;
    private readonly IGamesService _games;
    private readonly IUserAccountsService _userAccounts;
    private readonly IMobileSettingsService _mobileSettings;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<UserAccount> _allUsers = [];

    public ObservableCollection<UserAccount> Users { get; } = [];
    public ObservableCollection<InstalledGameRecord> Games { get; } = [];
    public ObservableCollection<string> GameVersions { get; } = [];
    public ObservableCollection<Lobby> Lobbies { get; } = [];
    public ObservableCollection<string> FrameworkVersions { get; } = [];
    public ObservableCollection<string> Hosts { get; } = ["desktop", "mobile"];

    [ObservableProperty]
    private string? _userFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchNormalCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateUrlCommand))]
    private UserAccount? _selectedUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchNormalCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateUrlCommand))]
    private string _password = DefaultPassword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchNormalCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateUrlCommand))]
    private InstalledGameRecord? _selectedGame;

    [ObservableProperty]
    private string? _selectedGameVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchNormalCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateUrlCommand))]
    private Lobby? _selectedLobby;

    [ObservableProperty]
    private string? _selectedFrameworkVersion;

    [ObservableProperty]
    private string _languageCode = "en";

    [ObservableProperty]
    private string _host = "desktop";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchNormalCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchIncognitoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateUrlCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyUrlCommand))]
    private string? _generatedUrl;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public LaunchViewModel(
        ILaunchService launch,
        IGamesService games,
        IUserAccountsService userAccounts,
        IMobileSettingsService mobileSettings,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _launch = launch;
        _games = games;
        _userAccounts = userAccounts;
        _mobileSettings = mobileSettings;
        _context = context;
        _messenger = messenger;
    }

    partial void OnUserFilterChanged(string? value) => ApplyUserFilter();

    partial void OnSelectedGameChanged(InstalledGameRecord? value)
    {
        GameVersions.Clear();
        foreach (var version in Services.VersionOrdering.Descending((value?.Versions ?? []).Select(v => v.Version)))
        {
            GameVersions.Add(version);
        }

        // Newest-first, so the freshest build is preselected.
        SelectedGameVersion = GameVersions.FirstOrDefault();

        // clientTypeId 40 = mobile client; preselect the matching host.
        Host = value?.ClientTypeId == 40 ? "mobile" : "desktop";
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando usuarios, juegos y lobbies…";

        try
        {
            var usersTask = _userAccounts.GetUserAccountsAsync();
            var gamesTask = _games.GetInstalledGameRecordsAsync();
            var lobbiesTask = _mobileSettings.GetLobbiesAsync();
            var titanTask = _mobileSettings.GetTitanVersionsAsync();
            await Task.WhenAll(usersTask, gamesTask, lobbiesTask, titanTask);

            _allUsers = usersTask.Result.DataObject ?? [];
            ApplyUserFilter();

            Games.Clear();
            foreach (var game in (gamesTask.Result.DataObject ?? [])
                     .OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.ClientId))
            {
                Games.Add(game);
            }

            Lobbies.Clear();
            foreach (var lobby in (lobbiesTask.Result.DataObject ?? [])
                     .OrderBy(l => l.FriendlyName, StringComparer.OrdinalIgnoreCase))
            {
                Lobbies.Add(lobby);
            }

            SelectedLobby = Lobbies.FirstOrDefault(l => l.FriendlyName == "DotCom") ?? Lobbies.FirstOrDefault();

            FrameworkVersions.Clear();
            foreach (var version in Services.VersionOrdering.Descending((titanTask.Result.DataObject ?? []).Select(t => t.AppVersion)))
            {
                FrameworkVersions.Add(version);
            }

            // Newest-first, so the latest Titan is preselected.
            SelectedFrameworkVersion = FrameworkVersions.FirstOrDefault();

            StatusMessage = $"{_allUsers.Count} usuarios, {Games.Count} juegos, {Lobbies.Count} lobbies.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudo cargar el ambiente: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanGenerate() =>
        !IsBusy
        && SelectedUser is not null
        && SelectedGame is not null
        && SelectedLobby is not null
        && !string.IsNullOrWhiteSpace(Password);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task GenerateUrlAsync() => GenerateCoreAsync();

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task LaunchNormalAsync()
    {
        var url = await GenerateCoreAsync();
        if (url is not null && OpenBrowser(url, incognito: false))
        {
            StatusMessage = "Juego lanzado en el navegador.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task LaunchIncognitoAsync()
    {
        var url = await GenerateCoreAsync();
        if (url is not null && OpenBrowser(url, incognito: true))
        {
            StatusMessage = "Juego lanzado en modo incógnito.";
        }
    }

    private bool CanCopyUrl() => !string.IsNullOrEmpty(GeneratedUrl);

    [RelayCommand(CanExecute = nameof(CanCopyUrl))]
    private void CopyUrl()
    {
        Clipboard.SetText(GeneratedUrl!);
        StatusMessage = "URL copiada al portapapeles.";
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private async Task<string?> GenerateCoreAsync()
    {
        var user = SelectedUser!;
        var game = SelectedGame!;
        var lobby = SelectedLobby!;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Generando URL de {game.DisplayName} para {user.LoginName}…";

        try
        {
            var response = await _launch.GetGameUrlAsync(new GameLaunchRequest
            {
                Username = user.LoginName,
                Password = Password.Trim(),
                LobbyName = lobby.FriendlyName ?? lobby.Name,
                ModuleId = game.ModuleId,
                ClientId = game.ClientId,
                GameVersion = SelectedGameVersion,
                LanguageCode = string.IsNullOrWhiteSpace(LanguageCode) ? "en" : LanguageCode.Trim(),
                Host = string.IsNullOrWhiteSpace(Host) ? "desktop" : Host.Trim(),
                Iframe = "false",
                Framework = "titan",
                FrameworkVersion = SelectedFrameworkVersion,
            });

            if (!response.Success || string.IsNullOrWhiteSpace(response.DataObject))
            {
                StatusMessage = null;
                ErrorMessage = $"El server no devolvió la URL: {Shorten(response.CustomMessage)}";
                return null;
            }

            GeneratedUrl = response.DataObject;
            StatusMessage = "URL generada.";
            return GeneratedUrl;
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"Error generando la URL: {Shorten(ex.Message)}";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool OpenBrowser(string url, bool incognito)
    {
        var error = Services.BrowserLauncher.Open(url, incognito);
        if (error is not null)
        {
            ErrorMessage = incognito ? $"{error} La URL quedó generada — usá \"Copiar URL\"." : error;
            return false;
        }

        return true;
    }

    private void ApplyUserFilter()
    {
        var filter = UserFilter?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allUsers.Take(200)
            : _allUsers.Where(u => u.LoginName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false).Take(200);

        Users.Clear();
        foreach (var user in filtered)
        {
            Users.Add(user);
        }
    }

    private static string Shorten(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "sin detalle";
        }

        var clean = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 250 ? clean : clean[..250] + "…";
    }
}
