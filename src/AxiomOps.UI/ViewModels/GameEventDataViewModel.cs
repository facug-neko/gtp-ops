using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>One play/event row of a user's game history.</summary>
public sealed class GameEventRow
{
    public required int TransactionNumber { get; init; }
    public required int EventNumber { get; init; }
    public int ModuleId { get; init; }
    public DateTimeOffset? TransactionTime { get; init; }

    /// <summary>Decoded XML payloads (empty string when the stream was empty).</summary>
    public required string EventData { get; init; }
    public required string GameData { get; init; }
    public required string StatsData { get; init; }

    /// <summary>Kind of event, pulled from the event XML's className for quick scanning.</summary>
    public string? EventKind { get; init; }
}

/// <summary>
/// Shows every play of a user with its EventData, GameState and StatsData.
/// Source: GET /UserAccounts/GameEventData — the three byte streams come
/// Base64-encoded XML, decoded here for display.
/// </summary>
public partial class GameEventDataViewModel : ObservableObject
{
    private readonly IUserAccountsService _userAccounts;
    private readonly IMessenger _messenger;

    private List<GameEventRow> _allRows = [];

    public ObservableCollection<GameEventRow> Rows { get; } = [];

    [ObservableProperty]
    private int _userId;

    [ObservableProperty]
    private string? _loginName;

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyEventDataCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyGameDataCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyStatsDataCommand))]
    private GameEventRow? _selectedRow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public GameEventDataViewModel(IUserAccountsService userAccounts, IMessenger messenger)
    {
        _userAccounts = userAccounts;
        _messenger = messenger;
    }

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    public async Task LoadAsync(int userId, string? loginName)
    {
        UserId = userId;
        LoginName = loginName;
        await RefreshAsync();
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando jugadas…";

        try
        {
            var response = await _userAccounts.GetGameEventDataAsync(UserId);

            _allRows =
            [
                .. (response.DataObject ?? []).Select(d =>
                {
                    var eventXml = Decode(d.EventDataByteStream);
                    return new GameEventRow
                    {
                        TransactionNumber = d.TransactionNumber,
                        EventNumber = d.EventNumber,
                        ModuleId = d.ModuleId,
                        TransactionTime = d.TransactionTime,
                        EventData = eventXml,
                        GameData = Decode(d.GameDataByteStream),
                        StatsData = Decode(d.StatsDataByteStream),
                        EventKind = ExtractEventKind(eventXml),
                    };
                }),
            ];

            ApplyFilter();
            SelectedRow = Rows.FirstOrDefault();

            var transactions = _allRows.Select(r => r.TransactionNumber).Distinct().Count();
            StatusMessage = $"{_allRows.Count} eventos en {transactions} transacciones.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron cargar las jugadas: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCopyEvent() => !string.IsNullOrEmpty(SelectedRow?.EventData);

    [RelayCommand(CanExecute = nameof(CanCopyEvent))]
    private void CopyEventData() => CopyToClipboard(SelectedRow!.EventData, "EventData");

    private bool CanCopyGame() => !string.IsNullOrEmpty(SelectedRow?.GameData);

    [RelayCommand(CanExecute = nameof(CanCopyGame))]
    private void CopyGameData() => CopyToClipboard(SelectedRow!.GameData, "GameState");

    private bool CanCopyStats() => !string.IsNullOrEmpty(SelectedRow?.StatsData);

    [RelayCommand(CanExecute = nameof(CanCopyStats))]
    private void CopyStatsData() => CopyToClipboard(SelectedRow!.StatsData, "StatsData");

    private void CopyToClipboard(string text, string what)
    {
        Clipboard.SetText(text);
        StatusMessage = $"{what} copiado al portapapeles.";
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new OpenUsersToolMessage());

    private void ApplyFilter()
    {
        var filter = FilterText?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allRows
            : _allRows.Where(r =>
                r.TransactionNumber.ToString().Contains(filter, StringComparison.Ordinal)
                || (r.EventKind?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || r.EventData.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.GameData.Contains(filter, StringComparison.OrdinalIgnoreCase));

        Rows.Clear();
        foreach (var row in filtered)
        {
            Rows.Add(row);
        }
    }

    /// <summary>Byte streams arrive Base64-encoded XML; fall back to the raw value.</summary>
    private static string Decode(string? stream)
    {
        if (string.IsNullOrWhiteSpace(stream))
        {
            return string.Empty;
        }

        return Base64Text.TryDecode(stream, out var decoded, out _) ? decoded : stream;
    }

    /// <summary>
    /// Pulls a short event name out of the event XML (e.g. "SpinEventData" from
    /// className="...VeyronEngine.EventDatas.SpinEventData") for the list column.
    /// </summary>
    private static string? ExtractEventKind(string eventXml)
    {
        const string Marker = "className=\"";
        var start = eventXml.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += Marker.Length;
        var end = eventXml.IndexOf('"', start);
        if (end < 0)
        {
            return null;
        }

        var className = eventXml[start..end];
        var lastDot = className.LastIndexOf('.');
        return lastDot >= 0 && lastDot < className.Length - 1 ? className[(lastDot + 1)..] : className;
    }
}
