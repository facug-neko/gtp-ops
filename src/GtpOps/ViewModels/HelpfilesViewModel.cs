using System.Collections.ObjectModel;
using AxiomOps.Compass;
using AxiomOps.Compass.Gtp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace GtpOps.ViewModels;

/// <summary>
/// Helpfile release-readiness for a game: for the latest helpfile of each payout
/// variant, runs the easy-help validation and shows a checklist (markets,
/// languages, translations, dropped-markets regression) plus an overall verdict.
/// </summary>
public partial class HelpfilesViewModel : ObservableObject
{
    private readonly IGtpPortalService _portal;
    private readonly IMessenger _messenger;

    public ObservableCollection<HelpfileReport> Reports { get; } = [];

    [ObservableProperty]
    private int _gameId;

    [ObservableProperty]
    private string? _gameName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllReleasable))]
    private int _releasableCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string? _headline;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public bool AllReleasable => TotalCount > 0 && ReleasableCount == TotalCount;

    public HelpfilesViewModel(IGtpPortalService portal, IMessenger messenger)
    {
        _portal = portal;
        _messenger = messenger;
    }

    public async Task LoadAsync(int gameId, string? gameName)
    {
        GameId = gameId;
        GameName = gameName;
        await RefreshAsync();
    }

    private static Version ParseVersion(string? v) => Version.TryParse(v, out var r) ? r : new Version(0, 0);

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        Reports.Clear();
        StatusMessage = "Buscando helpfiles del juego…";

        try
        {
            var helpfiles = await _portal.GetHelpfilesForGameAsync(GameId);

            // Latest helpfile per payout variant — what a release actually ships.
            var latest = helpfiles
                .GroupBy(h => h.PayoutVariant)
                .Select(g => g.OrderByDescending(h => ParseVersion(h.Version)).First())
                .OrderBy(h => h.PayoutVariant, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (latest.Count == 0)
            {
                Headline = "Este juego no tiene helpfiles en EasyHelp";
                StatusMessage = null;
                return;
            }

            StatusMessage = $"Validando {latest.Count} helpfile(s)…";

            var reports = await Task.WhenAll(latest.Select(async h =>
            {
                var validation = await _portal.ValidateHelpfileAsync(h.VersionedDocumentId);
                var missing = await _portal.GetMissingTranslationsCountAsync(h.VersionedDocumentId);

                // Content rules are ours, not GTP's — a failure here must not hide
                // the rest of the checklist, so degrade to "not analyzed".
                HelpfileContentAnalysis? content = null;
                try
                {
                    var strings = await _portal.GetHelpfileStringsAsync(h.VersionedDocumentId);
                    content = HelpfileContentRules.Analyze(strings);
                }
                catch (CompassException)
                {
                }

                return HelpfileReadiness.Evaluate(h, validation, missing, content);
            }));

            foreach (var report in reports.OrderBy(r => r.IsReleasable).ThenBy(r => r.Helpfile.PayoutVariant, StringComparer.OrdinalIgnoreCase))
            {
                Reports.Add(report);
            }

            TotalCount = Reports.Count;
            ReleasableCount = Reports.Count(r => r.IsReleasable);

            Headline = AllReleasable
                ? $"✔ Los {TotalCount} helpfiles están listos para release"
                : $"✗ {TotalCount - ReleasableCount} de {TotalCount} helpfile(s) con problemas";
            StatusMessage = $"Últimos por variante · {GameName}";
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
}
