using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace AxiomOps.UI.ViewModels;

public enum PlayRepositoryRowStatus
{
    Resolving,
    Ready,
    UserMissing,
    KeyInvalid,
    UrlFailed,
}

/// <summary>One pre-armed play: a testdata file matched to a game variant and a real user account.</summary>
public sealed partial class PlayRepositoryRow : ObservableObject
{
    public required string TestDataFile { get; init; }
    public required string ModuleId { get; init; }
    public string? ClientId { get; init; }
    public string? LoginName { get; init; }
    public string? GameDisplayName { get; init; }
    public string? Host { get; init; }
    public string? Market { get; init; }
    public string? Prize { get; init; }
    public string? Description { get; init; }

    [ObservableProperty]
    private string? _gameVersion;

    [ObservableProperty]
    private string? _url;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusFlag))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private PlayRepositoryRowStatus _status = PlayRepositoryRowStatus.Resolving;

    [ObservableProperty]
    private string? _detail;

    /// <summary>null while resolving, true when ready — matches HealthToBrushConverter's tri-state.</summary>
    public bool? StatusFlag => Status switch
    {
        PlayRepositoryRowStatus.Resolving => null,
        PlayRepositoryRowStatus.Ready => true,
        _ => false,
    };

    public string StatusLabel => Status switch
    {
        PlayRepositoryRowStatus.Resolving => "Resolviendo…",
        PlayRepositoryRowStatus.Ready => "Listo",
        PlayRepositoryRowStatus.UserMissing => "Usuario no encontrado",
        PlayRepositoryRowStatus.KeyInvalid => "Testdata sin <Key> válido",
        PlayRepositoryRowStatus.UrlFailed => "Error generando URL",
        _ => "?",
    };
}

/// <summary>
/// Builds the QA "play repository" for one game variant: every testdata file for
/// that exact moduleId+clientId, matched to a real user account (by the testdata's
/// &lt;Key&gt; loginName), with a ready-to-use launch URL for each — then exportable
/// to CSV for the spreadsheet QA works from. The game is picked from the installed
/// games list (same combo as Launch), which pins a single clientId — testdata for
/// another client variant of the same moduleId (e.g. mobile when desktop is picked)
/// is intentionally excluded, not turned into extra rows.
/// </summary>
public partial class PlayRepositoryViewModel : ObservableObject
{
    private const string DefaultPassword = "test1234$";
    private const int UrlParallelism = 6;

    private readonly TestDataCatalogService _testData;
    private readonly IGamesService _games;
    private readonly IUserAccountsService _userAccounts;
    private readonly IMobileSettingsService _mobileSettings;
    private readonly ILaunchService _launch;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<PlayRepositoryRow> _allRows = [];

    public ObservableCollection<PlayRepositoryRow> Rows { get; } = [];
    public ObservableCollection<Lobby> Lobbies { get; } = [];
    public ObservableCollection<InstalledGameRecord> Games { get; } = [];
    public ObservableCollection<string> GameVersions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private InstalledGameRecord? _selectedGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string? _selectedGameVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private Lobby? _selectedLobby;

    [ObservableProperty]
    private bool _showOnlyReady = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _readyCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private int _totalCount;

    public bool HasResults => TotalCount > 0;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public PlayRepositoryViewModel(
        TestDataCatalogService testData,
        IGamesService games,
        IUserAccountsService userAccounts,
        IMobileSettingsService mobileSettings,
        ILaunchService launch,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _testData = testData;
        _games = games;
        _userAccounts = userAccounts;
        _mobileSettings = mobileSettings;
        _launch = launch;
        _context = context;
        _messenger = messenger;
    }

    partial void OnShowOnlyReadyChanged(bool value) => ApplyFilter();

    partial void OnSelectedGameChanged(InstalledGameRecord? value)
    {
        GameVersions.Clear();
        foreach (var version in VersionOrdering.Descending((value?.Versions ?? []).Select(v => v.Version)))
        {
            GameVersions.Add(version);
        }

        // Newest-first, so the freshest build is preselected — same default as Launch,
        // but the user can now override it here.
        SelectedGameVersion = GameVersions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        ErrorMessage = null;
        StatusMessage = "Cargando juegos y mercados…";

        try
        {
            var gamesTask = _games.GetInstalledGameRecordsAsync();
            var lobbiesTask = _mobileSettings.GetLobbiesAsync();
            await Task.WhenAll(gamesTask, lobbiesTask);

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
            StatusMessage = "Elegí un juego y generá el repositorio.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron cargar los juegos/mercados: {ex.Message}";
        }
    }

    private bool CanGenerate() =>
        !IsBusy && SelectedGame is not null && SelectedLobby is not null && !string.IsNullOrEmpty(SelectedGameVersion);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var game = SelectedGame!;
        var lobby = SelectedLobby!;
        var gameVersion = SelectedGameVersion!;
        var moduleId = game.ModuleId;
        var clientId = game.ClientId;
        var moduleIdText = moduleId.ToString(CultureInfo.InvariantCulture);
        var clientIdText = clientId.ToString(CultureInfo.InvariantCulture);
        var host = game.ClientTypeId == 40 ? "mobile" : "desktop";
        var gameDisplayName = game.DisplayName ?? game.FriendlyName ?? game.InternalName;

        IsBusy = true;
        ErrorMessage = null;
        _allRows = [];
        Rows.Clear();
        ReadyCount = 0;
        TotalCount = 0;
        StatusMessage = $"Buscando testdatas de {gameDisplayName}…";

        try
        {
            var entries = await _testData.ListAllAsync();

            // Only this exact moduleId+clientId — testdata whose Key explicitly names a
            // DIFFERENT clientId belongs to another variant (e.g. mobile) and is skipped,
            // not turned into an extra row.
            var matches = new List<TestDataEntry>();
            var otherVariant = 0;

            foreach (var entry in entries)
            {
                if (entry.Key is not null)
                {
                    if (!ModuleIdMatches(entry.Key.ModuleId, moduleId))
                    {
                        continue;
                    }

                    if (int.TryParse(entry.Key.ClientId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyClientId)
                        && keyClientId != clientId)
                    {
                        otherVariant++;
                        continue;
                    }

                    matches.Add(entry);
                    continue;
                }

                if (entry.Node.Name?.StartsWith(moduleIdText + "_", StringComparison.Ordinal) ?? false)
                {
                    matches.Add(entry);
                }
            }

            if (matches.Count == 0)
            {
                StatusMessage = otherVariant > 0
                    ? $"No hay testdatas para {gameDisplayName} ({host}) — {otherVariant} pertenecen a otra variante del juego."
                    : $"No se encontraron testdatas para el módulo {moduleId}.";
                return;
            }

            StatusMessage = $"{matches.Count} testdata(s) encontrados. Resolviendo usuarios…";

            var usersTask = _userAccounts.GetUserAccountsAsync();
            var titanTask = _mobileSettings.GetTitanVersionsAsync();
            await Task.WhenAll(usersTask, titanTask);

            var usersByLogin = (usersTask.Result.DataObject ?? [])
                .Where(u => !string.IsNullOrWhiteSpace(u.LoginName))
                .GroupBy(u => u.LoginName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var latestTitan = VersionOrdering.Latest((titanTask.Result.DataObject ?? []).Select(t => t.AppVersion));

            // Build the row scaffold: resolve identity + user, but not the URL yet.
            var scaffold = new List<(PlayRepositoryRow Row, UserAccount? User)>();

            foreach (var entry in matches)
            {
                if (entry.Key is null)
                {
                    scaffold.Add((new PlayRepositoryRow
                    {
                        TestDataFile = entry.Node.Name ?? "?",
                        ModuleId = moduleIdText,
                        ClientId = clientIdText,
                        Prize = entry.Summary.Prize,
                        Description = entry.Summary.Description,
                        Status = PlayRepositoryRowStatus.KeyInvalid,
                        Detail = entry.ParseError ?? "Sin <Key> válido",
                    }, null));
                    continue;
                }

                var key = entry.Key;
                usersByLogin.TryGetValue(key.LoginName, out var user);

                scaffold.Add((new PlayRepositoryRow
                {
                    TestDataFile = entry.Node.Name ?? "?",
                    ModuleId = key.ModuleId,
                    ClientId = clientIdText,
                    LoginName = key.LoginName,
                    GameDisplayName = gameDisplayName,
                    Host = host,
                    Market = lobby.FriendlyName ?? lobby.Name,
                    GameVersion = gameVersion,
                    Prize = entry.Summary.Prize,
                    Description = entry.Summary.Description,
                    Status = user is null ? PlayRepositoryRowStatus.UserMissing : PlayRepositoryRowStatus.Resolving,
                    Detail = user is null ? $"No existe una cuenta con loginName \"{key.LoginName}\" en el ambiente." : null,
                }, user));
            }

            _allRows = [.. scaffold.Select(s => s.Row)];
            ApplyFilter();
            TotalCount = _allRows.Count;

            // Generate the launch URL for every row that has a matching user.
            var pending = scaffold.Where(s => s.Row.Status == PlayRepositoryRowStatus.Resolving).ToList();
            var done = 0;
            var gate = new SemaphoreSlim(UrlParallelism);

            await Task.WhenAll(pending.Select(async item =>
            {
                await gate.WaitAsync();
                try
                {
                    var response = await _launch.GetGameUrlAsync(new GameLaunchRequest
                    {
                        Username = item.User!.LoginName,
                        Password = DefaultPassword,
                        LobbyName = lobby.FriendlyName ?? lobby.Name,
                        ModuleId = moduleId,
                        ClientId = clientId,
                        GameVersion = gameVersion,
                        LanguageCode = "en",
                        Host = host,
                        Iframe = "false",
                        Framework = "titan",
                        FrameworkVersion = latestTitan,
                    });

                    if (response.Success && !string.IsNullOrWhiteSpace(response.DataObject))
                    {
                        item.Row.Url = response.DataObject;
                        item.Row.Status = PlayRepositoryRowStatus.Ready;
                    }
                    else
                    {
                        item.Row.Status = PlayRepositoryRowStatus.UrlFailed;
                        item.Row.Detail = Shorten(response.CustomMessage);
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    item.Row.Status = PlayRepositoryRowStatus.UrlFailed;
                    item.Row.Detail = Shorten(ex.Message);
                }
                finally
                {
                    gate.Release();
                    var n = Interlocked.Increment(ref done);
                    StatusMessage = $"Generando URLs… {n}/{pending.Count}";
                }
            }));

            ReadyCount = _allRows.Count(r => r.Status == PlayRepositoryRowStatus.Ready);
            var variantNote = otherVariant > 0 ? $" ({otherVariant} de otra variante omitidos)" : string.Empty;
            StatusMessage = $"{ReadyCount} de {TotalCount} link(s) listos para el repositorio{variantNote}.";
            ApplyFilter();
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"Error generando el repositorio: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExportCsv() => !IsBusy && _allRows.Count > 0;

    /// <summary>Exports the currently filtered rows to a CSV the QA spreadsheet can import.</summary>
    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar repositorio de jugadas",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"repositorio_{SelectedGame?.ModuleId}_{SelectedGame?.ClientId}.csv",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",",
                "ModuleId", "ClientId", "Juego", "TestData", "LoginName", "Prize", "Description",
                "Host", "Mercado", "Version", "Estado", "URL", "Detalle"));

            foreach (var row in Rows)
            {
                builder.AppendLine(string.Join(",",
                    CsvField(row.ModuleId),
                    CsvField(row.ClientId),
                    CsvField(row.GameDisplayName),
                    CsvField(row.TestDataFile),
                    CsvField(row.LoginName),
                    CsvField(row.Prize),
                    CsvField(row.Description),
                    CsvField(row.Host),
                    CsvField(row.Market),
                    CsvField(row.GameVersion),
                    CsvField(row.StatusLabel),
                    CsvField(row.Url),
                    CsvField(row.Detail)));
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            StatusMessage = $"Exportado: {Rows.Count} fila(s) en {Path.GetFileName(dialog.FileName)}.";
        }
        catch (IOException ex)
        {
            ErrorMessage = $"No se pudo guardar el archivo: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyUrl(PlayRepositoryRow row)
    {
        if (string.IsNullOrEmpty(row.Url))
        {
            return;
        }

        Clipboard.SetText(row.Url);
        StatusMessage = $"URL de {row.LoginName} copiada al portapapeles.";
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private void ApplyFilter()
    {
        var filtered = ShowOnlyReady
            ? _allRows.Where(r => r.Status == PlayRepositoryRowStatus.Ready)
            : _allRows;

        Rows.Clear();
        foreach (var row in filtered.OrderBy(r => r.Status == PlayRepositoryRowStatus.Ready ? 0 : 1)
                     .ThenBy(r => r.LoginName, StringComparer.OrdinalIgnoreCase))
        {
            Rows.Add(row);
        }

        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private static bool ModuleIdMatches(string keyModuleId, int moduleId) =>
        int.TryParse(keyModuleId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed == moduleId;

    private static string CsvField(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
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
