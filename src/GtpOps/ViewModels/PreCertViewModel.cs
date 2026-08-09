using System.Collections.ObjectModel;
using AxiomOps.Compass;
using AxiomOps.Compass.Gtp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace GtpOps.ViewModels;

/// <summary>Row of the pre-certification grid (one market × variant).</summary>
public sealed class PreCertRow
{
    public required string Variant { get; init; }
    public required string Market { get; init; }
    public required string MarketCode { get; init; }
    public required string Decision { get; init; }
    public required bool RequiresCertification { get; init; }
    public required string Characteristics { get; init; }
    public required string CurrentVersion { get; init; }
    public required string ActiveVersion { get; init; }

    /// <summary>true = ready (no cert needed), false = needs attention. Drives the row color.</summary>
    public bool IsReady => !RequiresCertification;
}

/// <summary>
/// Pre-certification readiness for a project: per market × payout variant, GTP's
/// decision on whether it requires certification before release (missing
/// certificate, service version change, etc.). Read-only.
/// </summary>
public partial class PreCertViewModel : ObservableObject
{
    private readonly IGtpPortalService _portal;
    private readonly IMessenger _messenger;

    private List<PreCertRow> _all = [];

    public ObservableCollection<PreCertRow> Rows { get; } = [];

    [ObservableProperty]
    private int _projectId;

    [ObservableProperty]
    private string? _projectName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _onlyRequiringCertification;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllReady))]
    private int _requiresCount;

    [ObservableProperty]
    private int _readyCount;

    [ObservableProperty]
    private string? _headline;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public bool AllReady => RequiresCount == 0 && _all.Count > 0;

    public PreCertViewModel(IGtpPortalService portal, IMessenger messenger)
    {
        _portal = portal;
        _messenger = messenger;
    }

    partial void OnOnlyRequiringCertificationChanged(bool value) => ApplyFilter();

    public async Task LoadAsync(int projectId, string? projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        await RefreshAsync();
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Consultando pre-certificación…";

        try
        {
            var result = await _portal.GetPreCertificationAsync(ProjectId);
            var markets = result?.Markets ?? [];

            _all =
            [
                .. markets
                    .Select(m => new PreCertRow
                    {
                        Variant = m.Variant ?? "—",
                        Market = m.Market ?? "—",
                        MarketCode = m.MarketCode ?? "—",
                        Decision = Humanize(m.Decision),
                        RequiresCertification = m.RequiresCertification,
                        Characteristics = string.Join(", ", m.Characteristics ?? []),
                        CurrentVersion = Blank(m.Metadata?.CurrentServiceVersion),
                        ActiveVersion = Blank(m.Metadata?.ActiveServiceVersion),
                    })
                    .OrderByDescending(r => r.RequiresCertification)
                    .ThenBy(r => r.Market, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Variant, StringComparer.OrdinalIgnoreCase),
            ];

            RequiresCount = _all.Count(r => r.RequiresCertification);
            ReadyCount = _all.Count - RequiresCount;

            Headline = _all.Count == 0
                ? "Sin datos de pre-certificación para este proyecto"
                : RequiresCount == 0
                    ? "✔ Todos los mercados están certificados / no requieren certificación"
                    : $"✗ {RequiresCount} de {_all.Count} combinaciones mercado×variante requieren certificación";

            ApplyFilter();
            StatusMessage = $"{_all.Count} combinaciones · {ReadyCount} listas · {RequiresCount} requieren certificación.";
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

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToProjectsMessage());

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _all.Where(r => !OnlyRequiringCertification || r.RequiresCertification))
        {
            Rows.Add(row);
        }
    }

    private static string Humanize(string? decision) => decision switch
    {
        "DoesNotRequireCertification" => "No requiere",
        "RequiresCertificationNewMarket" => "Mercado nuevo",
        "RequiresCertificationServiceVersionChange" => "Cambio de versión",
        null or "" => "—",
        _ => decision,
    };

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
