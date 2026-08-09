using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>Row of the hosts grid: basic info arrives fast, health/stats fill in later.</summary>
public sealed class HostRow
{
    public string? HostName { get; init; }
    public string? IpAddress { get; init; }
    public bool? IsHealthy { get; init; }
    public string CpuUsed { get; init; } = "…";
    public string RamUsed { get; init; } = "…";
}

/// <summary>Row of the services grid (one Windows service on one host).</summary>
public sealed class ServiceRow
{
    public required string HostName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? State { get; init; }

    public bool IsRunning => string.Equals(State, "Running", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Row of the application-pools grid (one IIS app pool on one host).</summary>
public sealed class AppPoolRow
{
    public required string HostName { get; init; }
    public required string Name { get; init; }
    public string? Status { get; init; }

    // IIS reports app-pool state as "Started"/"Stopped".
    public bool IsStarted => string.Equals(Status, "Started", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Environment dashboard. Loads in two independent stages so the screen is
/// usable immediately: the cheap endpoints (games, host entries, provision
/// date) resolve in ~1s, while GET /Health — which takes ~35-40s because the
/// SERVER walks every host querying IIS live — fills in health, failures and
/// per-host stats in the background.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IHealthService _health;
    private readonly IGamesService _games;
    private readonly IEnvironmentsService _environments;
    private readonly IManageService _manage;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<string> _windowsHosts = [];

    public ObservableCollection<GameRecord> InstalledGames { get; } = [];
    public ObservableCollection<HostRow> Hosts { get; } = [];
    public ObservableCollection<ServiceRow> Services { get; } = [];
    public ObservableCollection<AppPoolRow> AppPools { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoadingBasics;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyPropertyChangedFor(nameof(HealthLabel))]
    private bool _isLoadingHealth;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHealthy))]
    [NotifyPropertyChangedFor(nameof(HealthLabel))]
    [NotifyPropertyChangedFor(nameof(Failures))]
    private ApplianceState? _state;

    [ObservableProperty]
    private string _lastProvision = "—";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartVeyronServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopVeyronServicesCommand))]
    private bool _isLoadingServices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStoppedServices))]
    private int _stoppedServicesCount;

    [ObservableProperty]
    private string? _servicesSummary;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshAppPoolsCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartAllAppPoolsCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAllAppPoolsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecycleAllAppPoolsCommand))]
    private bool _isLoadingAppPools;

    [ObservableProperty]
    private string? _appPoolsSummary;

    public bool HasStoppedServices => StoppedServicesCount > 0;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public bool? IsHealthy => State?.ApplianceHealth?.IsHealthy;

    public string HealthLabel => IsLoadingHealth
        ? "Analizando…"
        : IsHealthy switch
        {
            true => "Saludable",
            false => "Con problemas",
            _ => "—",
        };

    public int InstalledGamesCount => InstalledGames.Count;

    public int HostCount => Hosts.Count;

    /// <summary>Service / website / app-pool failures flattened for display.</summary>
    public IReadOnlyList<string> Failures
    {
        get
        {
            var health = State?.ApplianceHealth;
            if (health is null)
            {
                return [];
            }

            static IEnumerable<string> Describe(string kind, IEnumerable<NameStatus>? failures) =>
                failures?.Select(f => $"{kind}: {f.Name} ({f.Status})") ?? [];

            return
            [
                .. Describe("Servicio", health.ServiceFailures),
                .. Describe("Sitio IIS", health.WebsiteFailures),
                .. Describe("App pool", health.ApplicationPoolFailures),
            ];
        }
    }

    public DashboardViewModel(
        IHealthService health,
        IGamesService games,
        IEnvironmentsService environments,
        IManageService manage,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _health = health;
        _games = games;
        _environments = environments;
        _manage = manage;
        _context = context;
        _messenger = messenger;
    }

    private bool CanRefresh() => !IsLoadingBasics && !IsLoadingHealth;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        ErrorMessage = null;

        // Both stages run concurrently; the fast one paints the screen right away.
        // Services depend on the host list, so they chain after the basics.
        await Task.WhenAll(LoadBasicsThenServicesAsync(), LoadHealthAsync());
    }

    private async Task LoadBasicsThenServicesAsync()
    {
        await LoadBasicsAsync();

        // Services and app pools both need the Windows host list; run them together.
        await Task.WhenAll(LoadServicesAsync(), LoadAppPoolsAsync());
    }

    private async Task LoadBasicsAsync()
    {
        IsLoadingBasics = true;

        try
        {
            var gamesTask = _games.GetInstalledDatabaseGameRecordsAsync();
            var hostsTask = _health.GetApplianceHostEntriesAsync();
            var provisionTask = _environments.GetLastProvisionDateAsync();
            await Task.WhenAll(gamesTask, hostsTask, provisionTask);

            InstalledGames.Clear();
            foreach (var game in (gamesTask.Result.DataObject ?? [])
                     .OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.ClientId))
            {
                InstalledGames.Add(game);
            }
            OnPropertyChanged(nameof(InstalledGamesCount));

            // Basic host rows now; health/stats replace them when /Health lands.
            var entries = hostsTask.Result.DataObject?.HostFileEntries ?? [];
            if (Hosts.Count == 0)
            {
                foreach (var entry in entries)
                {
                    Hosts.Add(new HostRow { HostName = entry.HostName, IpAddress = entry.IpAddress });
                }
                OnPropertyChanged(nameof(HostCount));
            }

            // Only Windows hosts expose service management.
            _windowsHosts = [.. entries.Where(e => e.IsMicrosoftWindows && !string.IsNullOrWhiteSpace(e.HostName)).Select(e => e.HostName!)];

            LastProvision = provisionTask.Result.DataObject?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"No se pudo cargar la información básica: {ex.Message}";
        }
        finally
        {
            IsLoadingBasics = false;
        }
    }

    private async Task LoadHealthAsync()
    {
        IsLoadingHealth = true;

        try
        {
            var response = await _health.GetApplianceStateAsync();
            State = response.DataObject;

            if (!response.Success)
            {
                ErrorMessage = response.CustomMessage ?? "El ambiente no devolvió success.";
            }

            RebuildHostRows();
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error consultando la salud del ambiente: {ex.Message}";
        }
        finally
        {
            IsLoadingHealth = false;
        }
    }

    private void RebuildHostRows()
    {
        var statistics = State?.Statistics;
        if (statistics is null or { Count: 0 })
        {
            return;
        }

        Hosts.Clear();
        foreach (var host in statistics)
        {
            var metrics = host.Performance?.Metrics;
            Hosts.Add(new HostRow
            {
                HostName = host.Host?.HostName,
                IpAddress = host.Host?.IpAddress,
                IsHealthy = host.HostHealth?.IsHealthy,
                CpuUsed = FormatPercent(metrics?.Cpu?.Percent?.Used?.Value),
                RamUsed = FormatPercent(metrics?.Ram?.Percent?.Used?.Value),
            });
        }
        OnPropertyChanged(nameof(HostCount));
    }

    private static string FormatPercent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "—";
        }

        return double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? $"{value:F1} %"
            : raw;
    }

    private bool CanRefreshServices() => !IsLoadingServices;

    [RelayCommand(CanExecute = nameof(CanRefreshServices))]
    private Task RefreshServicesAsync() => LoadServicesAsync();

    private async Task LoadServicesAsync()
    {
        if (_windowsHosts.Count == 0)
        {
            return;
        }

        IsLoadingServices = true;

        try
        {
            var perHost = await Task.WhenAll(_windowsHosts.Select(async host =>
                (Host: host, Info: await _manage.GetServiceInfoAsync(host))));

            var rows = perHost
                .SelectMany(r => (r.Info.DataObject ?? []).Select(s => new ServiceRow
                {
                    HostName = r.Host,
                    Name = s.Name ?? "?",
                    Description = string.IsNullOrWhiteSpace(s.Description) || s.Description == "None" ? null : s.Description,
                    State = s.State,
                }))
                .OrderBy(r => r.IsRunning) // stopped first — they're the reason this panel exists
                .ThenBy(r => r.HostName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Services.Clear();
            foreach (var row in rows)
            {
                Services.Add(row);
            }

            StoppedServicesCount = rows.Count(r => !r.IsRunning);
            ServicesSummary = $"{rows.Count} servicios en {_windowsHosts.Count} host(s)" +
                              (StoppedServicesCount > 0 ? $" — {StoppedServicesCount} detenido(s)" : " — todos corriendo");
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"No se pudieron cargar los servicios: {ex.Message}";
        }
        finally
        {
            IsLoadingServices = false;
        }
    }

    private bool CanBulkService() => !IsLoadingServices;

    [RelayCommand(CanExecute = nameof(CanBulkService))]
    private Task StartAllServicesAsync() => BulkServiceActionAsync(start: true, veyronOnly: false);

    [RelayCommand(CanExecute = nameof(CanBulkService))]
    private Task StopAllServicesAsync() => BulkServiceActionAsync(start: false, veyronOnly: false);

    [RelayCommand(CanExecute = nameof(CanBulkService))]
    private Task StartVeyronServicesAsync() => BulkServiceActionAsync(start: true, veyronOnly: true);

    [RelayCommand(CanExecute = nameof(CanBulkService))]
    private Task StopVeyronServicesAsync() => BulkServiceActionAsync(start: false, veyronOnly: true);

    /// <summary>
    /// Starts/stops a set of services. Veyron scope = every service whose name
    /// contains "veyron" (case-insensitive) — the ones to stop before uploading
    /// a game service build.
    /// </summary>
    private async Task BulkServiceActionAsync(bool start, bool veyronOnly)
    {
        var scope = veyronOnly ? "de Veyron" : "del ambiente";

        var targets = Services
            .Where(s => !veyronOnly || s.Name.Contains("veyron", StringComparison.OrdinalIgnoreCase))
            .Where(s => start ? !s.IsRunning : s.IsRunning)
            .ToList();

        if (targets.Count == 0)
        {
            ServicesSummary = start
                ? $"No hay servicios {scope} detenidos para iniciar."
                : $"No hay servicios {scope} corriendo para detener.";
            return;
        }

        if (!start)
        {
            var confirmation = System.Windows.MessageBox.Show(
                $"¿Detener {targets.Count} servicio(s) {scope}?\n\n" +
                string.Join("\n", targets.Take(12).Select(t => $"  • {t.Name} ({t.HostName})")) +
                (targets.Count > 12 ? $"\n  … y {targets.Count - 12} más." : string.Empty),
                "Confirmar stop masivo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmation != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        IsLoadingServices = true;
        ErrorMessage = null;
        ServicesSummary = $"{(start ? "Iniciando" : "Deteniendo")} {targets.Count} servicio(s) {scope}…";

        var failures = new List<string>();
        var gate = new SemaphoreSlim(4);

        try
        {
            await Task.WhenAll(targets.Select(async row =>
            {
                await gate.WaitAsync();
                try
                {
                    var result = start
                        ? await _manage.StartServiceAsync(row.HostName, row.Name)
                        : await _manage.StopServiceAsync(row.HostName, row.Name);

                    var outcome = result.DataObject;
                    if (!result.Success || outcome is { Result: false })
                    {
                        lock (failures)
                        {
                            failures.Add($"{row.Name} ({row.HostName}): {outcome?.Message ?? result.CustomMessage ?? "sin detalle"}");
                        }
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    lock (failures)
                    {
                        failures.Add($"{row.Name} ({row.HostName}): {ex.Message}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));

            if (failures.Count > 0)
            {
                ErrorMessage = $"{failures.Count} servicio(s) fallaron al {(start ? "iniciar" : "detener")}:\n" +
                               string.Join("\n", failures.Take(3)) +
                               (failures.Count > 3 ? $"\n… y {failures.Count - 3} más." : string.Empty);
            }
        }
        finally
        {
            IsLoadingServices = false;
        }

        await LoadServicesAsync();
    }

    [RelayCommand]
    private async Task StartServiceAsync(ServiceRow row)
    {
        await RunServiceActionAsync(row, start: true);
    }

    [RelayCommand]
    private async Task StopServiceAsync(ServiceRow row)
    {
        var confirmation = System.Windows.MessageBox.Show(
            $"¿Detener el servicio \"{row.Name}\" en {row.HostName}?",
            "Confirmar stop",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation == System.Windows.MessageBoxResult.Yes)
        {
            await RunServiceActionAsync(row, start: false);
        }
    }

    private async Task RunServiceActionAsync(ServiceRow row, bool start)
    {
        IsLoadingServices = true;
        ErrorMessage = null;

        try
        {
            var result = start
                ? await _manage.StartServiceAsync(row.HostName, row.Name)
                : await _manage.StopServiceAsync(row.HostName, row.Name);

            var outcome = result.DataObject;
            if (!result.Success || outcome is { Result: false })
            {
                ErrorMessage = $"No se pudo {(start ? "iniciar" : "detener")} {row.Name} en {row.HostName}: " +
                               (outcome?.Message ?? result.CustomMessage ?? "sin detalle");
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error sobre {row.Name} en {row.HostName}: {ex.Message}";
        }
        finally
        {
            IsLoadingServices = false;
        }

        await LoadServicesAsync();
    }

    // ----- Application pools -----

    private bool CanRefreshAppPools() => !IsLoadingAppPools;

    [RelayCommand(CanExecute = nameof(CanRefreshAppPools))]
    private Task RefreshAppPoolsAsync() => LoadAppPoolsAsync();

    private async Task LoadAppPoolsAsync()
    {
        if (_windowsHosts.Count == 0)
        {
            return;
        }

        IsLoadingAppPools = true;

        try
        {
            var perHost = await Task.WhenAll(_windowsHosts.Select(async host =>
                (Host: host, Info: await _manage.GetAppPoolInfoAsync(host))));

            var rows = perHost
                .SelectMany(r => (r.Info.DataObject ?? []).Select(p => new AppPoolRow
                {
                    HostName = r.Host,
                    Name = p.Name ?? "?",
                    Status = p.Status,
                }))
                .OrderBy(r => r.IsStarted) // stopped first — the ones that need attention
                .ThenBy(r => r.HostName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AppPools.Clear();
            foreach (var row in rows)
            {
                AppPools.Add(row);
            }

            var stopped = rows.Count(r => !r.IsStarted);
            AppPoolsSummary = $"{rows.Count} app pools en {_windowsHosts.Count} host(s)" +
                              (stopped > 0 ? $" — {stopped} detenido(s)" : " — todos iniciados");
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"No se pudieron cargar los application pools: {ex.Message}";
        }
        finally
        {
            IsLoadingAppPools = false;
        }
    }

    private bool CanBulkAppPool() => !IsLoadingAppPools;

    [RelayCommand(CanExecute = nameof(CanBulkAppPool))]
    private Task StartAllAppPoolsAsync() => BulkAppPoolActionAsync(AppPoolAction.Start);

    [RelayCommand(CanExecute = nameof(CanBulkAppPool))]
    private Task StopAllAppPoolsAsync() => BulkAppPoolActionAsync(AppPoolAction.Stop);

    [RelayCommand(CanExecute = nameof(CanBulkAppPool))]
    private Task RecycleAllAppPoolsAsync() => BulkAppPoolActionAsync(AppPoolAction.Recycle);

    private enum AppPoolAction { Start, Stop, Recycle }

    private async Task BulkAppPoolActionAsync(AppPoolAction action)
    {
        var targets = action switch
        {
            AppPoolAction.Start => AppPools.Where(p => !p.IsStarted).ToList(),
            AppPoolAction.Stop => AppPools.Where(p => p.IsStarted).ToList(),
            _ => AppPools.Where(p => p.IsStarted).ToList(), // recycle only applies to started pools
        };

        if (targets.Count == 0)
        {
            AppPoolsSummary = action switch
            {
                AppPoolAction.Start => "No hay app pools detenidos para iniciar.",
                AppPoolAction.Stop => "No hay app pools iniciados para detener.",
                _ => "No hay app pools iniciados para reciclar.",
            };
            return;
        }

        var verb = action switch { AppPoolAction.Start => "Iniciar", AppPoolAction.Stop => "Detener", _ => "Reciclar" };

        if (action is AppPoolAction.Stop or AppPoolAction.Recycle)
        {
            var confirmation = System.Windows.MessageBox.Show(
                $"¿{verb} {targets.Count} app pool(s)?\n\n" +
                string.Join("\n", targets.Take(12).Select(t => $"  • {t.Name} ({t.HostName})")) +
                (targets.Count > 12 ? $"\n  … y {targets.Count - 12} más." : string.Empty),
                $"Confirmar {verb.ToLowerInvariant()} masivo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmation != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        IsLoadingAppPools = true;
        ErrorMessage = null;
        AppPoolsSummary = $"{verb} {targets.Count} app pool(s)…";

        var failures = new List<string>();
        var gate = new SemaphoreSlim(4);

        try
        {
            await Task.WhenAll(targets.Select(async pool =>
            {
                await gate.WaitAsync();
                try
                {
                    var result = await RunAppPoolAsync(pool, action);
                    if (!result.Success)
                    {
                        lock (failures)
                        {
                            failures.Add($"{pool.Name} ({pool.HostName}): {result.CustomMessage ?? "sin detalle"}");
                        }
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    lock (failures)
                    {
                        failures.Add($"{pool.Name} ({pool.HostName}): {ex.Message}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));

            if (failures.Count > 0)
            {
                ErrorMessage = $"{failures.Count} app pool(s) fallaron al {verb.ToLowerInvariant()}:\n" +
                               string.Join("\n", failures.Take(3)) +
                               (failures.Count > 3 ? $"\n… y {failures.Count - 3} más." : string.Empty);
            }
        }
        finally
        {
            IsLoadingAppPools = false;
        }

        await LoadAppPoolsAsync();
    }

    [RelayCommand]
    private Task StartAppPool(AppPoolRow row) => RunSingleAppPoolAsync(row, AppPoolAction.Start);

    [RelayCommand]
    private async Task StopAppPool(AppPoolRow row)
    {
        var confirmation = System.Windows.MessageBox.Show(
            $"¿Detener el app pool \"{row.Name}\" en {row.HostName}?",
            "Confirmar stop",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation == System.Windows.MessageBoxResult.Yes)
        {
            await RunSingleAppPoolAsync(row, AppPoolAction.Stop);
        }
    }

    [RelayCommand]
    private Task RecycleAppPool(AppPoolRow row) => RunSingleAppPoolAsync(row, AppPoolAction.Recycle);

    private async Task RunSingleAppPoolAsync(AppPoolRow row, AppPoolAction action)
    {
        IsLoadingAppPools = true;
        ErrorMessage = null;

        var verb = action switch { AppPoolAction.Start => "iniciar", AppPoolAction.Stop => "detener", _ => "reciclar" };

        try
        {
            var result = await RunAppPoolAsync(row, action);
            if (!result.Success)
            {
                ErrorMessage = $"No se pudo {verb} {row.Name} en {row.HostName}: {result.CustomMessage ?? "sin detalle"}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error al {verb} {row.Name} en {row.HostName}: {ex.Message}";
        }
        finally
        {
            IsLoadingAppPools = false;
        }

        await LoadAppPoolsAsync();
    }

    private Task<AxiomResponse<bool>> RunAppPoolAsync(AppPoolRow row, AppPoolAction action) => action switch
    {
        AppPoolAction.Start => _manage.StartAppPoolAsync(row.HostName, row.Name),
        AppPoolAction.Stop => _manage.StopAppPoolAsync(row.HostName, row.Name),
        _ => _manage.RecycleAppPoolAsync(row.HostName, row.Name),
    };

    [RelayCommand]
    private void ChangeEnvironment() => _messenger.Send(new ChangeEnvironmentRequestedMessage());

    [RelayCommand]
    private void OpenBetSettingsTool() => _messenger.Send(new OpenBetSettingsToolMessage());

    [RelayCommand]
    private void OpenUsersTool() => _messenger.Send(new OpenUsersToolMessage());

    [RelayCommand]
    private void OpenGamesTool() => _messenger.Send(new OpenGamesToolMessage());

    [RelayCommand]
    private void OpenLaunchTool() => _messenger.Send(new OpenLaunchToolMessage());

    [RelayCommand]
    private void OpenFilesTool() => _messenger.Send(new OpenFilesToolMessage());

    [RelayCommand]
    private void OpenLogsTool() => _messenger.Send(new OpenLogsToolMessage());

    [RelayCommand]
    private void OpenTestDataTool() => _messenger.Send(new OpenTestDataToolMessage());

    [RelayCommand]
    private void OpenDeployGame() => _messenger.Send(new OpenDeployGameMessage());
}
