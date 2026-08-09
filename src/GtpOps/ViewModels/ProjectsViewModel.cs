using System.Collections.ObjectModel;
using System.Globalization;
using AxiomOps.Compass;
using AxiomOps.Compass.Gtp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GtpOps.Services;

namespace GtpOps.ViewModels;

/// <summary>
/// Read-only project browser: pick a game (catalog or raw gameId) → list its
/// projects → drill into a project's detail and releases.
/// </summary>
public partial class ProjectsViewModel : ObservableObject
{
    private readonly IGtpPortalService _portal;
    private readonly IGameCatalog _catalog;
    private readonly IMessenger _messenger;

    public ObservableCollection<GtpGame> Games { get; } = [];
    public ObservableCollection<GtpProject> Projects { get; } = [];
    public ObservableCollection<GtpRelease> Releases { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProjectsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateHelpfilesCommand))]
    private GtpGame? _selectedGame;

    /// <summary>Raw gameId entry, for games not in the catalog.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProjectsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateHelpfilesCommand))]
    private string _manualGameId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectParticipants))]
    [NotifyCanExecuteChangedFor(nameof(ValidateDeliverablesCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreCertificationCommand))]
    private GtpProject? _selectedProject;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProjectsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoadingReleases;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string ProjectParticipants =>
        SelectedProject?.GameProjectParticipants is { Count: > 0 } list
            ? string.Join(", ", list.Select(p => p.User?.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
            : "—";

    public ProjectsViewModel(IGtpPortalService portal, IGameCatalog catalog, IMessenger messenger)
    {
        _portal = portal;
        _catalog = catalog;
        _messenger = messenger;
    }

    private bool CanValidateHelpfiles() => !IsBusy && ResolveGameId() is not null;

    [RelayCommand(CanExecute = nameof(CanValidateHelpfiles))]
    private void ValidateHelpfiles()
    {
        var gameId = ResolveGameId()!.Value;
        var name = SelectedGame?.DisplayName ?? $"gameId {gameId}";
        _messenger.Send(new OpenHelpfilesMessage(gameId, name));
    }

    private bool CanValidateDeliverables() => SelectedProject is not null;

    [RelayCommand(CanExecute = nameof(CanValidateDeliverables))]
    private void ValidateDeliverables()
    {
        var project = SelectedProject!;
        _messenger.Send(new OpenDeliverablesMessage(project.Id, ResolveGameId() ?? 0, project.ProjectName));
    }

    [RelayCommand(CanExecute = nameof(CanValidateDeliverables))]
    private void PreCertification()
    {
        var project = SelectedProject!;
        _messenger.Send(new OpenPreCertMessage(project.Id, project.ProjectName));
    }

    partial void OnSelectedGameChanged(GtpGame? value)
    {
        if (value is not null)
        {
            ManualGameId = string.Empty;
        }
    }

    partial void OnSelectedProjectChanged(GtpProject? value)
    {
        Releases.Clear();
        if (value is not null)
        {
            _ = LoadReleasesAsync(value.Id);
        }
    }

    [RelayCommand]
    private Task LoadCatalogAsync()
    {
        Games.Clear();
        foreach (var game in _catalog.Games)
        {
            Games.Add(game);
        }

        StatusMessage = $"{Games.Count} juegos en el catálogo. Elegí uno o ingresá un gameId.";
        return Task.CompletedTask;
    }

    private int? ResolveGameId()
    {
        if (!string.IsNullOrWhiteSpace(ManualGameId)
            && int.TryParse(ManualGameId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var manual)
            && manual > 0)
        {
            return manual;
        }

        return SelectedGame?.GameId;
    }

    private bool CanLoadProjects() => !IsBusy && ResolveGameId() is not null;

    [RelayCommand(CanExecute = nameof(CanLoadProjects))]
    private async Task LoadProjectsAsync()
    {
        var gameId = ResolveGameId()!.Value;
        var label = SelectedGame?.DisplayName ?? $"gameId {gameId}";

        IsBusy = true;
        ErrorMessage = null;
        Projects.Clear();
        Releases.Clear();
        SelectedProject = null;
        StatusMessage = $"Cargando proyectos de {label}…";

        try
        {
            var projects = await _portal.GetProjectsForGameAsync(gameId);
            foreach (var project in projects.OrderByDescending(p => p.CreatedOn))
            {
                Projects.Add(project);
            }

            StatusMessage = $"{Projects.Count} proyecto(s) para {label}.";
            SelectedProject = Projects.FirstOrDefault();
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

    private async Task LoadReleasesAsync(int projectId)
    {
        IsLoadingReleases = true;

        try
        {
            var releases = await _portal.GetReleasesAsync(projectId);
            Releases.Clear();
            foreach (var release in releases.OrderByDescending(r => r.DateSubmitted))
            {
                Releases.Add(release);
            }
        }
        catch (CompassException ex)
        {
            ErrorMessage = ex.LooksLikeAuthProblem
                ? "Sesión de Compass vencida — reautenticá con compass login."
                : $"No se pudieron cargar los releases: {ex.Message}";
        }
        finally
        {
            IsLoadingReleases = false;
        }
    }
}
