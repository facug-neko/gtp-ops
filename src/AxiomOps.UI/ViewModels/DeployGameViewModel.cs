using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace AxiomOps.UI.ViewModels;

/// <summary>A service that was running before the deploy and must be restored afterwards.</summary>
internal sealed record StoppedService(string HostName, string Name);

/// <summary>
/// Manual game deploy: stop services → upload the game-service zip
/// (POST /Upload/GameService, the portal's "Install a game → Service files")
/// → bring the services back. Only the services that were RUNNING beforehand
/// are restarted, and the restart runs even if the upload fails, so the
/// environment is never left down.
/// </summary>
public partial class DeployGameViewModel : ObservableObject
{
    private const int Parallelism = 4;

    /// <summary>Services whose name contains this are the ones that lock the game-service files.</summary>
    private const string VeyronMarker = "veyron";

    /// <summary>
    /// The stop endpoint returns as soon as the request is accepted, but Windows
    /// keeps the process alive (and its file handles open) for a moment longer —
    /// uploading too early fails with "the file is being used by another process".
    /// </summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Even once every service reports "Stopped", Windows can take a few more
    /// seconds to actually unload the process and release its DLL handles — the
    /// SCM state flips before the file locks are gone. 3s wasn't enough in
    /// practice (uploads still hit "being used by another process"), so this is
    /// now a real margin, backed up by the upload retry below as a second line
    /// of defense.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(10);

    /// <summary>How many times to retry the upload if it fails on a file lock.</summary>
    private const int MaxUploadAttempts = 4;
    private static readonly TimeSpan UploadRetryDelay = TimeSpan.FromSeconds(8);

    private readonly IManageService _manage;
    private readonly IHealthService _health;
    private readonly IUploadService _upload;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeployCommand))]
    private string? _zipPath;

    [ObservableProperty]
    private string? _zipDescription;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeployCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickZipCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public DeployGameViewModel(
        IManageService manage,
        IHealthService health,
        IUploadService upload,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _manage = manage;
        _health = health;
        _upload = upload;
        _context = context;
        _messenger = messenger;
    }

    private bool CanPickZip() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanPickZip))]
    private void PickZip()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí el zip del game service",
            Filter = "Zip (*.zip)|*.zip|Todos (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ZipPath = dialog.FileName;
        var info = new FileInfo(dialog.FileName);
        ZipDescription = $"{info.Name}  ({info.Length / 1024d / 1024d:F1} MB)";
        ErrorMessage = null;
    }

    private bool CanDeploy() => !IsBusy && !string.IsNullOrWhiteSpace(ZipPath) && File.Exists(ZipPath);

    [RelayCommand(CanExecute = nameof(CanDeploy))]
    private async Task DeployAsync()
    {
        var zip = ZipPath!;
        var fileName = Path.GetFileName(zip);

        var confirmation = System.Windows.MessageBox.Show(
            $"Se va a deployar en {EnvironmentName}:\n\n" +
            "  1. Detener los servicios de Veyron\n" +
            "  2. Esperar a que queden efectivamente detenidos\n" +
            $"  3. Subir {fileName}\n" +
            "  4. Volver a levantar los servicios detenidos\n\n" +
            "¿Continuar?",
            "Confirmar deploy",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        Log.Clear();

        List<StoppedService> stopped = [];
        var uploadOk = false;

        try
        {
            // 1. Snapshot what is running now, so we restore exactly this state.
            StatusMessage = "Leyendo el estado de los servicios…";
            Append("① Leyendo el estado actual de los servicios…");

            var running = await GetRunningVeyronServicesAsync();
            if (running.Count == 0)
            {
                Append("   No hay servicios de Veyron corriendo (se sigue con la subida).");
            }
            else
            {
                Append($"   {running.Count} servicio(s) de Veyron corriendo.");
            }

            // 2. Stop them and wait until Windows really released them.
            if (running.Count > 0)
            {
                StatusMessage = $"Deteniendo {running.Count} servicios…";
                Append($"② Deteniendo {running.Count} servicio(s) de Veyron…");

                var stopFailures = await RunOnServicesAsync(running, start: false);
                stopped = [.. running.Except(stopFailures)];

                Append($"   Solicitud de stop aceptada: {stopped.Count}/{running.Count}.");
                foreach (var failure in stopFailures.Take(5))
                {
                    Append($"   ✗ No se pudo detener {failure.Name} ({failure.HostName})");
                }

                if (stopFailures.Count > 0)
                {
                    Append("   ⚠ Con servicios sin detener la subida puede fallar por archivos en uso.");
                }

                StatusMessage = "Esperando a que los servicios queden detenidos…";
                Append("   Esperando a que queden efectivamente detenidos…");

                var allStopped = await WaitUntilStoppedAsync(running);
                if (allStopped)
                {
                    Append($"   ✔ Todos detenidos. Margen de {SettleDelay.TotalSeconds:F0}s para liberar archivos…");
                    await Task.Delay(SettleDelay);
                }
                else
                {
                    Append($"   ⚠ Timeout de {StopTimeout.TotalSeconds:F0}s esperando el frenado; se intenta subir igual.");
                }
            }

            // 3. Upload the zip. Retried on a file-lock error: the settle delay above
            // covers the common case, but under load Windows can hold the handles
            // even longer, so this is the real safety net.
            StatusMessage = $"Subiendo {fileName}…";
            Append($"③ Subiendo {fileName}…");

            for (var attempt = 1; attempt <= MaxUploadAttempts; attempt++)
            {
                string? failureDetail = null;
                var isFileLock = false;

                try
                {
                    await using var stream = File.OpenRead(zip);
                    var result = await _upload.UploadGameServiceAsync(stream, fileName);

                    if (result.Success)
                    {
                        uploadOk = true;
                        Append("   ✔ Subida correcta.");
                        break;
                    }

                    failureDetail = Shorten(result.CustomMessage);
                    isFileLock = LooksLikeFileLock(result.CustomMessage);
                    Append($"   ✗ El server rechazó la subida: {failureDetail}");
                }
                catch (AxiomApiException ex)
                {
                    failureDetail = AxiomErrorText.Describe(ex);
                    isFileLock = LooksLikeFileLock(failureDetail);
                    Append($"   ✗ Error subiendo: {failureDetail}");
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    failureDetail = Shorten(ex.Message);
                    isFileLock = LooksLikeFileLock(ex.Message);
                    Append($"   ✗ Error subiendo: {failureDetail}");
                }

                if (isFileLock && attempt < MaxUploadAttempts)
                {
                    Append($"   ⏳ Archivo todavía en uso — reintento {attempt + 1}/{MaxUploadAttempts} en {UploadRetryDelay.TotalSeconds:F0}s…");
                    StatusMessage = $"Archivo en uso, reintentando ({attempt + 1}/{MaxUploadAttempts})…";
                    await Task.Delay(UploadRetryDelay);
                    continue;
                }

                ErrorMessage = $"La subida falló: {failureDetail}";
                break;
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            Append($"✗ Error durante el deploy: {Shorten(ex.Message)}");
            ErrorMessage = $"Error durante el deploy: {Shorten(ex.Message)}";
        }
        finally
        {
            // 4. Always bring back what we took down, even if the upload failed.
            if (stopped.Count > 0)
            {
                StatusMessage = $"Levantando {stopped.Count} servicios…";
                Append($"④ Levantando {stopped.Count} servicio(s)…");

                var startFailures = await RunOnServicesAsync(stopped, start: true);
                var restarted = stopped.Count - startFailures.Count;
                Append($"   Levantados: {restarted}/{stopped.Count}.");

                foreach (var failure in startFailures.Take(5))
                {
                    Append($"   ✗ No se pudo levantar {failure.Name} ({failure.HostName})");
                }

                if (startFailures.Count > 0)
                {
                    ErrorMessage = (ErrorMessage is null ? string.Empty : ErrorMessage + "\n") +
                        $"⚠ Quedaron {startFailures.Count} servicio(s) sin levantar — revisalos en el dashboard.";
                }
            }

            StatusMessage = uploadOk
                ? $"✔ Deploy terminado: {fileName} subido y servicios restaurados."
                : "Deploy finalizado con errores — revisá el detalle.";
            Append(uploadOk ? "✔ Deploy completo." : "✗ Deploy finalizado con errores.");

            IsBusy = false;
        }
    }

    /// <summary>Veyron services currently running on the appliance's Windows hosts.</summary>
    private async Task<List<StoppedService>> GetRunningVeyronServicesAsync()
    {
        var states = await GetVeyronServiceStatesAsync();

        return
        [
            .. states
                .Where(kv => IsBusyState(kv.Value))
                .Select(kv => kv.Key),
        ];
    }

    /// <summary>Current state of every Veyron service, keyed by host + name.</summary>
    private async Task<Dictionary<StoppedService, string?>> GetVeyronServiceStatesAsync()
    {
        var entries = await _health.GetApplianceHostEntriesAsync();
        var hosts = (entries.DataObject?.HostFileEntries ?? [])
            .Where(h => h.IsMicrosoftWindows && !string.IsNullOrWhiteSpace(h.HostName))
            .Select(h => h.HostName!)
            .ToList();

        var perHost = await Task.WhenAll(hosts.Select(async host =>
            (Host: host, Info: await _manage.GetServiceInfoAsync(host))));

        return perHost
            .SelectMany(r => (r.Info.DataObject ?? []).Select(s => (r.Host, Service: s)))
            .Where(x => x.Service.Name?.Contains(VeyronMarker, StringComparison.OrdinalIgnoreCase) ?? false)
            .ToDictionary(x => new StoppedService(x.Host, x.Service.Name!), x => x.Service.State);
    }

    /// <summary>
    /// True while a service still holds its files: "Running" or any transitional
    /// *Pending state. Only a real "Stopped" frees the game-service DLLs.
    /// </summary>
    private static bool IsBusyState(string? state) =>
        string.IsNullOrWhiteSpace(state)
        || state.Equals("Running", StringComparison.OrdinalIgnoreCase)
        || state.EndsWith("Pending", StringComparison.OrdinalIgnoreCase);

    /// <summary>Polls until every given service reports Stopped, or the timeout elapses.</summary>
    private async Task<bool> WaitUntilStoppedAsync(IReadOnlyCollection<StoppedService> services)
    {
        var deadline = DateTime.UtcNow + StopTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            Dictionary<StoppedService, string?> states;
            try
            {
                states = await GetVeyronServiceStatesAsync();
            }
            catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
            {
                continue; // transient while services cycle — keep polling
            }

            var pending = services
                .Where(s => states.TryGetValue(s, out var state) && IsBusyState(state))
                .ToList();

            if (pending.Count == 0)
            {
                return true;
            }

            StatusMessage = $"Esperando a que se detengan {pending.Count} servicio(s)…";
        }

        return false;
    }

    /// <summary>Starts or stops the given services; returns the ones that failed.</summary>
    private async Task<List<StoppedService>> RunOnServicesAsync(IReadOnlyCollection<StoppedService> services, bool start)
    {
        var failures = new List<StoppedService>();
        var gate = new SemaphoreSlim(Parallelism);

        await Task.WhenAll(services.Select(async service =>
        {
            await gate.WaitAsync();
            try
            {
                var result = start
                    ? await _manage.StartServiceAsync(service.HostName, service.Name)
                    : await _manage.StopServiceAsync(service.HostName, service.Name);

                if (!result.Success || result.DataObject is { Result: false })
                {
                    lock (failures) { failures.Add(service); }
                }
            }
            catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
            {
                lock (failures) { failures.Add(service); }
            }
            finally
            {
                gate.Release();
            }
        }));

        return failures;
    }

    /// <summary>True for the classic Windows "file in use" message the locked DLLs produce.</summary>
    private static bool LooksLikeFileLock(string? message) =>
        message is not null
        && (message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private void Append(string line) => Log.Add($"[{DateTime.Now:HH:mm:ss}] {line}");

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
