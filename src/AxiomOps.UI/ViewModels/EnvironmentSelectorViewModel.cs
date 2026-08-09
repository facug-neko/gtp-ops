using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using AxiomOps.Compass;
using AxiomOps.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// First screen: lists environments from Compass (Okta auth handled by the
/// CLI), lets the user pick one, resolves/validates its Axiom api-key against
/// a cheap read endpoint, and connects. Mirrors the preflight flow of the
/// axiom-compass tool, including invalidating stale stored keys on 401/403.
/// </summary>
public partial class EnvironmentSelectorViewModel : ObservableObject
{
    private readonly ICompassCliService _compass;
    private readonly IAxiomKeyStore _keyStore;
    private readonly AxiomEnvironmentContext _context;
    private readonly IGameSettingsService _gameSettings;
    private readonly IMessenger _messenger;

    private List<CompassEnvironment> _allEnvironments = [];

    public ObservableCollection<CompassEnvironment> Environments { get; } = [];

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    private bool _axiomOnly = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private CompassEnvironment? _selectedEnvironment;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadEnvironmentsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public EnvironmentSelectorViewModel(
        ICompassCliService compass,
        IAxiomKeyStore keyStore,
        AxiomEnvironmentContext context,
        IGameSettingsService gameSettings,
        IMessenger messenger)
    {
        _compass = compass;
        _keyStore = keyStore;
        _context = context;
        _gameSettings = gameSettings;
        _messenger = messenger;
    }

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    partial void OnAxiomOnlyChanged(bool value) => ApplyFilter();

    partial void OnSelectedEnvironmentChanged(CompassEnvironment? value)
    {
        ErrorMessage = null;
        ApiKey = value is null ? string.Empty : _keyStore.GetKey(value.InternalName) ?? string.Empty;
    }

    private bool CanLoad() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadEnvironmentsAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Consultando ambientes vía Compass…";

        try
        {
            _allEnvironments = await _compass.GetEnvironmentsAsync();
            ApplyFilter();
            StatusMessage = $"{_allEnvironments.Count} ambientes ({_allEnvironments.Count(e => e.IsAxiom)} Axiom).";
        }
        catch (CompassException ex)
        {
            StatusMessage = null;
            ErrorMessage = ex.LooksLikeAuthProblem
                ? $"{ex.Message}\n\nProbablemente falte autenticarse: corré `compass login` en una terminal y reintentá."
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConnect() =>
        !IsBusy && SelectedEnvironment is not null && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        var environment = SelectedEnvironment!;
        var key = ApiKey.Trim();

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Validando api-key contra {environment.InternalName}…";

        _context.SetEnvironment(environment.InternalName, environment.AxiomCoreBaseUrl, key);

        try
        {
            // Cheap read — the same endpoint axiom-compass uses to validate keys.
            await _gameSettings.GetGameProvidersAsync();

            _keyStore.SaveKey(environment.InternalName, key);
            StatusMessage = $"Conectado a {environment.InternalName}.";
            _messenger.Send(new EnvironmentConnectedMessage(environment.InternalName));
        }
        catch (AxiomApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Stale stored key (env regenerated) — drop it so the next attempt prompts fresh.
            if (_keyStore.GetKey(environment.InternalName) == key)
            {
                _keyStore.DeleteKey(environment.InternalName);
            }

            _context.Clear();
            StatusMessage = null;
            ErrorMessage =
                $"{environment.InternalName} rechazó la api-key (HTTP {(int)ex.StatusCode!}). " +
                "Puede haber sido regenerada — pedí la key vigente al owner del ambiente.";
        }
        catch (AxiomApiException ex)
        {
            _context.Clear();
            StatusMessage = null;
            ErrorMessage = $"Error del ambiente: {ex.Message}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _context.Clear();
            StatusMessage = null;
            ErrorMessage = $"No se pudo conectar a {environment.InternalName}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var filter = FilterText?.Trim();

        var filtered = _allEnvironments
            .Where(e => !AxiomOnly || e.IsAxiom)
            .Where(e => string.IsNullOrEmpty(filter)
                        || e.InternalName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || (e.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(e => e.InternalName, StringComparer.OrdinalIgnoreCase);

        Environments.Clear();
        foreach (var environment in filtered)
        {
            Environments.Add(environment);
        }
    }
}
