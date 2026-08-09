using System.Collections.ObjectModel;
using AxiomOps.Compass;
using AxiomOps.Compass.Gtp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GtpOps.Services;

namespace GtpOps.ViewModels;

/// <summary>One stage circle in the stepper.</summary>
public sealed class StageRow
{
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required bool? IsComplete { get; init; } // true=green, false=red, null=no requirements (gray)
}

/// <summary>
/// Deliverables validator for a project (V2 only). Per-stage traffic light plus
/// three expandable sections — faltantes / cargados / opcionales — each row
/// drills into its variant×market coverage. submission-status is ignored (lies);
/// requirement.isRequirementMet rules.
/// </summary>
public partial class DeliverablesViewModel : ObservableObject
{
    private readonly IGtpPortalService _portal;
    private readonly IGameCatalog _catalog;
    private readonly IDeliverableOverrideStore _overrides;
    private readonly IBackendDeliverableStore _backend;
    private readonly IMessenger _messenger;

    private List<GtpDeliverable> _raw = [];

    public ObservableCollection<StageRow> Stages { get; } = [];
    public ObservableCollection<DeliverableItem> All { get; } = [];
    public ObservableCollection<DeliverableItem> MissingRequired { get; } = [];
    public ObservableCollection<DeliverableItem> Loaded { get; } = [];
    public ObservableCollection<DeliverableItem> OptionalPending { get; } = [];
    public ObservableCollection<DeliverableItem> Discarded { get; } = [];
    public ObservableCollection<DeliverableItem> Backend { get; } = [];

    [ObservableProperty]
    private int _projectId;

    [ObservableProperty]
    private int _gameId;

    [ObservableProperty]
    private string? _projectName;

    /// <summary>Studio/provider whose personal discard rules apply.</summary>
    [ObservableProperty]
    private string _scope = "General";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    private bool _hasNoDeliverables;

    [ObservableProperty]
    private bool _allRequiredMet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllTabHeader))]
    private int _allCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MissingTabHeader))]
    private int _missingCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscardedTabHeader))]
    private int _discardedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackendTabHeader))]
    [NotifyPropertyChangedFor(nameof(BackendSummary))]
    private int _backendCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackendSummary))]
    [NotifyPropertyChangedFor(nameof(BackendAllDone))]
    private int _backendMissingCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackendSummary))]
    private int _backendLoadedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadedTabHeader))]
    private int _loadedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptionalTabHeader))]
    private int _optionalCount;

    [ObservableProperty]
    private string? _headline;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public bool ShowResults => !HasNoDeliverables;

    public string AllTabHeader => $"Todos ({AllCount})";
    public string MissingTabHeader => $"Faltantes ({MissingCount})";
    public string LoadedTabHeader => $"Cargados ({LoadedCount})";
    public string OptionalTabHeader => $"Opcionales ({OptionalCount})";
    public string DiscardedTabHeader => $"Descartados ({DiscardedCount})";
    // UI label is generic ("Míos") so any area can track their own deliverables;
    // the internal identifiers stay "Backend" (harmless implementation detail).
    public string BackendTabHeader => $"★ Míos ({BackendCount})";

    public string BackendSummary => BackendCount == 0
        ? "Marcá deliverables como míos (★) para hacerles seguimiento acá."
        : $"{BackendLoadedCount} cargado(s) · {BackendMissingCount} faltante(s) · {BackendCount} míos en total";

    public bool BackendAllDone => BackendCount > 0 && BackendMissingCount == 0;

    public DeliverablesViewModel(
        IGtpPortalService portal,
        IGameCatalog catalog,
        IDeliverableOverrideStore overrides,
        IBackendDeliverableStore backend,
        IMessenger messenger)
    {
        _portal = portal;
        _catalog = catalog;
        _overrides = overrides;
        _backend = backend;
        _messenger = messenger;
    }

    public async Task LoadAsync(int projectId, int gameId, string? projectName)
    {
        ProjectId = projectId;
        GameId = gameId;
        ProjectName = projectName;
        Scope = _catalog.ResolveScope(gameId);
        await RefreshAsync();
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Validando deliverables (V2)…";

        try
        {
            _raw = await _portal.GetDeliverablesAsync(ProjectId);
            Recompute();
        }
        catch (CompassException ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.LooksLikeAuthProblem
                ? $"{ex.Message}\n\nReautenticá con: compass login --provider gamesglobal --write"
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-runs validation over the cached deliverables with the current discards.</summary>
    private void Recompute()
    {
        Stages.Clear();
        All.Clear();
        MissingRequired.Clear();
        Loaded.Clear();
        OptionalPending.Clear();
        Discarded.Clear();
        Backend.Clear();

        var result = DeliverableValidation.Validate(_raw, _overrides.GetDiscarded(Scope), _backend.GetBackendTypeIds());

        HasNoDeliverables = result.HasNoDeliverables;
        AllRequiredMet = result.AllRequiredMet;

        if (result.HasNoDeliverables)
        {
            Headline = "Este proyecto no tiene deliverables en V2";
            StatusMessage = "La API V2 no devolvió deliverables para este proyecto (puede gestionarse en la V1).";
            return;
        }

        foreach (var stage in result.Stages)
        {
            Stages.Add(new StageRow
            {
                Name = DeliverableStageNames.Display(stage.Stage),
                Detail = stage.HasRequirements ? $"{stage.RequiredMet}/{stage.RequiredTotal}" : "—",
                IsComplete = stage.HasRequirements ? stage.IsComplete : null,
            });
        }

        foreach (var item in result.Items) { All.Add(item); }
        foreach (var item in result.MissingRequired) { MissingRequired.Add(item); }
        foreach (var item in result.Loaded) { Loaded.Add(item); }
        foreach (var item in result.OptionalPending) { OptionalPending.Add(item); }
        foreach (var item in result.Discarded) { Discarded.Add(item); }
        // Backend: pending first, then loaded — the "what do I still owe" glance.
        foreach (var item in result.Backend.OrderBy(i => i.IsMet)) { Backend.Add(item); }

        AllCount = All.Count;
        MissingCount = MissingRequired.Count;
        LoadedCount = Loaded.Count;
        OptionalCount = OptionalPending.Count;
        DiscardedCount = Discarded.Count;
        BackendCount = result.Backend.Count;
        BackendMissingCount = result.BackendMissing.Count;
        BackendLoadedCount = result.BackendLoaded.Count;

        Headline = result.AllRequiredMet
            ? "✔ Todos los deliverables obligatorios (según tus reglas) están cargados"
            : $"✗ Faltan {MissingCount} deliverable(s) obligatorio(s)";
        StatusMessage = $"{result.TotalDeliverables} deliverables · {result.TotalFiles} archivo(s) · reglas de «{Scope}»" +
                        (DiscardedCount > 0 ? $" · {DiscardedCount} descartado(s)" : string.Empty);
    }

    /// <summary>Marks the deliverable type as not-mandatory-for-us in the current scope.</summary>
    [RelayCommand]
    private void Discard(DeliverableItem? item)
    {
        if (item is null)
        {
            return;
        }

        _overrides.Discard(Scope, item.DeliverableTypeId);
        Recompute();
    }

    /// <summary>Restores a discarded deliverable type back to GTP's rule.</summary>
    [RelayCommand]
    private void Restore(DeliverableItem? item)
    {
        if (item is null)
        {
            return;
        }

        _overrides.Restore(Scope, item.DeliverableTypeId);
        Recompute();
    }

    /// <summary>Toggles the backend (mine) tag for the deliverable type (global).</summary>
    [RelayCommand]
    private void ToggleBackend(DeliverableItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.IsBackend)
        {
            _backend.Unmark(item.DeliverableTypeId);
        }
        else
        {
            _backend.Mark(item.DeliverableTypeId);
        }

        Recompute();
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToProjectsMessage());
}
