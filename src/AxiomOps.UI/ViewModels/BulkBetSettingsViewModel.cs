using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// Bulk tool: applies one bet-setting value for a game (moduleId/clientId) to
/// every user of the environment. Per user it mirrors the portal's manual flow:
/// GET UserGameBetSettings → replace the value in the `user` list → PATCH with
/// validate=true (default list untouched).
/// </summary>
public partial class BulkBetSettingsViewModel : ObservableObject
{
    private const int MaxParallelism = 6;

    private readonly IGamesService _games;
    private readonly IBetSettingsService _betSettings;
    private readonly IUserAccountsService _userAccounts;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<UserAccount> _allUsers = [];
    private CancellationTokenSource? _runCts;

    public ObservableCollection<GameRecord> Games { get; } = [];
    public ObservableCollection<SettingOption> Settings { get; } = [];
    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyToAllCommand))]
    private GameRecord? _selectedGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyToAllCommand))]
    private SettingOption? _selectedSetting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyToAllCommand))]
    private string _newValue = string.Empty;

    [ObservableProperty]
    private string? _loginFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyToAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax = 1;

    [ObservableProperty]
    private int _succeeded;

    [ObservableProperty]
    private int _failed;

    [ObservableProperty]
    private int _skipped;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public int TotalUsers => _allUsers.Count;

    public BulkBetSettingsViewModel(
        IGamesService games,
        IBetSettingsService betSettings,
        IUserAccountsService userAccounts,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _games = games;
        _betSettings = betSettings;
        _userAccounts = userAccounts;
        _context = context;
        _messenger = messenger;
    }

    partial void OnSelectedGameChanged(GameRecord? value)
    {
        Settings.Clear();
        SelectedSetting = null;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando juegos y usuarios del ambiente…";

        try
        {
            var gamesTask = _games.GetInstalledDatabaseGameRecordsAsync();
            var usersTask = _userAccounts.GetUserAccountsAsync();
            await Task.WhenAll(gamesTask, usersTask);

            Games.Clear();
            foreach (var game in (gamesTask.Result.DataObject ?? [])
                     .OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.ClientId))
            {
                Games.Add(game);
            }

            _allUsers = usersTask.Result.DataObject ?? [];
            OnPropertyChanged(nameof(TotalUsers));
            StatusMessage = $"{Games.Count} juegos, {_allUsers.Count} usuarios.";
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

    private bool CanLoadSettings() => !IsBusy && SelectedGame is not null;

    /// <summary>Fetches the setting catalog for the game using a sample user.</summary>
    [RelayCommand(CanExecute = nameof(CanLoadSettings))]
    private async Task LoadSettingsAsync()
    {
        var game = SelectedGame!;

        IsBusy = true;
        ErrorMessage = null;
        Settings.Clear();
        SelectedSetting = null;
        StatusMessage = $"Buscando settings de {game.DisplayName}…";

        try
        {
            UserGameBetSettings? sample = null;
            string? lastError = null;

            foreach (var user in FilteredUsers().Take(5))
            {
                try
                {
                    var response = await _betSettings.GetUserGameBetSettingsAsync(user.UserId, game.ModuleId, game.ClientId);
                    if (response.Success && response.DataObject is not null)
                    {
                        sample = response.DataObject;
                        break;
                    }

                    lastError = response.CustomMessage;
                }
                catch (AxiomApiException ex)
                {
                    lastError = ex.Message;
                }
            }

            if (sample is null)
            {
                StatusMessage = null;
                ErrorMessage = $"No se pudieron obtener settings para {game.DisplayName}. Último error: {lastError ?? "sin detalle"}";
                return;
            }

            var catalog = (sample.User is { Count: > 0 } ? sample.User : sample.Default) ?? [];
            foreach (var setting in catalog.OrderBy(s => s.SettingName, StringComparer.OrdinalIgnoreCase))
            {
                Settings.Add(new SettingOption(setting));
            }

            StatusMessage = $"{Settings.Count} settings disponibles.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSettingChanged(SettingOption? value)
    {
        if (value is not null)
        {
            NewValue = value.CurrentValueText;
        }
    }

    private bool CanApplyToAll() =>
        !IsBusy && SelectedGame is not null && SelectedSetting is not null && !string.IsNullOrWhiteSpace(NewValue);

    [RelayCommand(CanExecute = nameof(CanApplyToAll))]
    private async Task ApplyToAllAsync()
    {
        var game = SelectedGame!;
        var setting = SelectedSetting!;

        BetSettingValue newValue;
        try
        {
            newValue = BuildValue(setting, NewValue.Trim());
        }
        catch (FormatException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        var targets = FilteredUsers().ToList();
        if (targets.Count == 0)
        {
            ErrorMessage = "No hay usuarios que coincidan con el filtro.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Vas a aplicar:\n\n" +
            $"    {setting.Name} (settingId {setting.SettingId}) = {NewValue.Trim()}\n\n" +
            $"en {game.DisplayName} [{game.ModuleId}/{game.ClientId}]\n" +
            $"para {targets.Count} usuarios de {EnvironmentName}.\n\n¿Continuar?",
            "Confirmar cambio masivo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        IsApplying = true;
        ErrorMessage = null;
        Log.Clear();
        Succeeded = Failed = Skipped = ProgressValue = 0;
        ProgressMax = targets.Count;
        StatusMessage = $"Aplicando a {targets.Count} usuarios…";

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        // Marshals per-user results back to the UI thread.
        var progress = new Progress<(string Kind, string? Message)>(report =>
        {
            ProgressValue++;
            switch (report.Kind)
            {
                case "ok": Succeeded++; break;
                case "skip": Skipped++; break;
                default: Failed++; break;
            }

            if (report.Message is not null)
            {
                Log.Insert(0, report.Message);
            }

            StatusMessage = $"Procesados {ProgressValue}/{ProgressMax} — OK {Succeeded}, fallidos {Failed}, salteados {Skipped}.";
        });

        try
        {
            await Task.Run(() => Parallel.ForEachAsync(
                targets,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = token },
                async (user, ct) => await ApplyToUserAsync(user, game, setting, newValue, progress, ct)), token);

            StatusMessage = $"Terminado: OK {Succeeded}, fallidos {Failed}, salteados {Skipped} (de {targets.Count}).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Cancelado en {ProgressValue}/{ProgressMax} — OK {Succeeded}, fallidos {Failed}, salteados {Skipped}.";
        }
        finally
        {
            _runCts.Dispose();
            _runCts = null;
            IsApplying = false;
            IsBusy = false;
        }
    }

    private async Task ApplyToUserAsync(
        UserAccount user,
        GameRecord game,
        SettingOption setting,
        BetSettingValue newValue,
        IProgress<(string, string?)> progress,
        CancellationToken cancellationToken)
    {
        var who = $"{user.LoginName} (id {user.UserId})";

        try
        {
            var response = await _betSettings.GetUserGameBetSettingsAsync(user.UserId, game.ModuleId, game.ClientId, cancellationToken);
            if (!response.Success || response.DataObject is null)
            {
                progress.Report(("skip", $"⤼ {who}: fetch sin success — {Shorten(response.CustomMessage)}"));
                return;
            }

            var data = response.DataObject;
            var userSettings = data.User ?? [];

            var entry = userSettings.FirstOrDefault(s => s.SettingId == setting.SettingId);
            if (entry is null)
            {
                var template = data.Default?.FirstOrDefault(s => s.SettingId == setting.SettingId);
                if (template is null)
                {
                    progress.Report(("skip", $"⤼ {who}: el juego no expone el settingId {setting.SettingId}."));
                    return;
                }

                entry = new BetSetting
                {
                    SettingId = template.SettingId,
                    SettingName = template.SettingName,
                    SettingDescription = template.SettingDescription,
                    DataType = template.DataType,
                };
                userSettings.Add(entry);
            }

            entry.SettingValue = newValue;

            var patch = new SetUserGameBetSettingsRequest
            {
                UserId = user.UserId,
                ModuleId = game.ModuleId,
                ClientId = game.ClientId,
                Default = data.Default,
                User = userSettings,
                Validate = true,
            };

            var result = await _betSettings.SetUserGameBetSettingsAsync(patch, cancellationToken);
            if (result.Success)
            {
                progress.Report(("ok", null));
            }
            else
            {
                progress.Report(("fail", $"✗ {who}: {Shorten(result.CustomMessage)}"));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException)
        {
            progress.Report(("fail", $"✗ {who}: {Shorten(ex.Message)}"));
        }
    }

    private bool CanCancel() => IsApplying;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _runCts?.Cancel();

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private IEnumerable<UserAccount> FilteredUsers()
    {
        var filter = LoginFilter?.Trim();
        return string.IsNullOrEmpty(filter)
            ? _allUsers
            : _allUsers.Where(u => u.LoginName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Builds the settingValue payload with only the field matching the setting's data type.</summary>
    private static BetSettingValue BuildValue(SettingOption setting, string input)
    {
        var value = new BetSettingValue { SettingId = setting.SettingId };
        var ci = CultureInfo.InvariantCulture;

        switch (setting.DataType)
        {
            case 1: // boolean
                if (!bool.TryParse(input, out var b))
                {
                    throw new FormatException($"\"{input}\" no es un booleano válido (true/false).");
                }
                value.Boolean = b;
                break;

            case 2: // date
            case 3: // dateTime
                if (!DateTimeOffset.TryParse(input, ci, DateTimeStyles.AssumeUniversal, out var dt))
                {
                    throw new FormatException($"\"{input}\" no es una fecha válida.");
                }
                if (setting.DataType == 2) { value.Date = dt; } else { value.DateTime = dt; }
                break;

            case 4: // integer
                if (!long.TryParse(input, NumberStyles.Integer, ci, out var l))
                {
                    throw new FormatException($"\"{input}\" no es un entero válido.");
                }
                value.Integer = l;
                break;

            case 5: // money
                if (!decimal.TryParse(input, NumberStyles.Number, ci, out var m))
                {
                    throw new FormatException($"\"{input}\" no es un monto válido (usar punto decimal, ej. 40 o 0.2).");
                }
                value.Money = m;
                break;

            case 6: // percentage
                if (!double.TryParse(input, NumberStyles.Number, ci, out var p))
                {
                    throw new FormatException($"\"{input}\" no es un porcentaje válido (usar punto decimal).");
                }
                value.Percentage = p;
                break;

            case 8: // time
                value.Time = input;
                break;

            default: // 7 = string u otros
                value.String = input;
                break;
        }

        return value;
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

/// <summary>Row for the setting selector: id, name, type and current sample value.</summary>
public sealed class SettingOption(BetSetting source)
{
    public int SettingId { get; } = source.SettingId;
    public string Name { get; } = source.SettingName ?? $"Setting {source.SettingId}";
    public string? Description { get; } = source.SettingDescription;
    public int DataType { get; } = source.DataType;

    public string DataTypeLabel => DataType switch
    {
        1 => "boolean",
        2 => "date",
        3 => "dateTime",
        4 => "integer",
        5 => "money",
        6 => "percentage",
        8 => "time",
        _ => "string",
    };

    public string CurrentValueText
    {
        get
        {
            var v = source.SettingValue;
            var ci = CultureInfo.InvariantCulture;
            return v switch
            {
                { Boolean: not null } => v.Boolean.Value ? "true" : "false",
                { Integer: not null } => v.Integer.Value.ToString(ci),
                { Money: not null } => v.Money.Value.ToString(ci),
                { Percentage: not null } => v.Percentage.Value.ToString(ci),
                { String: not null } => v.String,
                { Date: not null } => v.Date.Value.ToString("yyyy-MM-dd", ci),
                { DateTime: not null } => v.DateTime.Value.ToString("O", ci),
                { Time: not null } => v.Time,
                _ => string.Empty,
            };
        }
    }

    public override string ToString() => $"[{SettingId}] {Name} ({DataTypeLabel}) = {CurrentValueText}";
}
