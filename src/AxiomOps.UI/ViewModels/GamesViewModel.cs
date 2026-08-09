using System.Collections.ObjectModel;
using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// Games list with install diagnostics: every installed game gets the
/// "Troubleshoot Install" verdict (database / files / dependencies) and the
/// list flags the ones with missing pieces.
/// </summary>
public partial class GamesViewModel : ObservableObject
{
    private readonly GameInstallDiagnosticsService _diagnostics;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<GameInstallDiagnosis> _all = [];

    public ObservableCollection<GameInstallDiagnosis> Games { get; } = [];

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    private GameInstallDiagnosis? _selectedGame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems))]
    [NotifyPropertyChangedFor(nameof(AllHealthy))]
    private int _problemCount;

    public bool HasProblems => ProblemCount > 0;

    public bool AllHealthy => !HasProblems && _all.Count > 0;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public GamesViewModel(GameInstallDiagnosticsService diagnostics, AxiomEnvironmentContext context, IMessenger messenger)
    {
        _diagnostics = diagnostics;
        _context = context;
        _messenger = messenger;
    }

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private Task InitializeAsync() => LoadAsync();

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Analizando instalación de los juegos…";

        try
        {
            _all = await _diagnostics.AnalyzeAsync();
            ProblemCount = _all.Count(d => !d.IsHealthy);
            ApplyFilter();
            StatusMessage = $"{_all.Count} juegos analizados.";
            SelectedGame = Games.FirstOrDefault();
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudo analizar la instalación: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private void ApplyFilter()
    {
        var filter = FilterText?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _all
            : _all.Where(d =>
                (d.DisplayName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || d.ModuleId.ToString().Contains(filter, StringComparison.Ordinal)
                || d.ClientId.ToString().Contains(filter, StringComparison.Ordinal)
                || (d.GameProvider?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));

        Games.Clear();
        foreach (var diagnosis in filtered)
        {
            Games.Add(diagnosis);
        }
    }
}
