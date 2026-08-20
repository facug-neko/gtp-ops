using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>How the line list is filtered.</summary>
public enum LogFilterMode
{
    All,
    ProblemsOnly,
    ErrorsOnly,
}

/// <summary>
/// Dedicated .log viewer for the environment's <c>C:\MGSLog</c> folder. Unlike the
/// general file manager, this is read-only and tuned for spotting errors: files are
/// listed newest-first, each line is colour-coded by severity, and the view can be
/// narrowed to just errors/warnings or a search term. It can also tail the selected
/// file on a timer.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    /// <summary>Where MGS logs live on the appliance. Editable in case an env differs.</summary>
    public const string DefaultLogFolder = @"C:\MGSLog";

    private readonly IManageService _manage;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;
    private readonly DispatcherTimer _tailTimer;

    private IReadOnlyList<LogLine> _allLines = [];
    private List<FileFolderNode> _allFiles = [];

    public ObservableCollection<FileFolderNode> Files { get; } = [];
    public ObservableCollection<LogLine> Lines { get; } = [];

    public IReadOnlyList<LogFilterMode> FilterModes { get; } =
        [LogFilterMode.All, LogFilterMode.ProblemsOnly, LogFilterMode.ErrorsOnly];

    /// <summary>Raised after the content reloads so the view can scroll to the newest line.</summary>
    public event Action? ScrolledToEndRequested;

    [ObservableProperty]
    private string _logFolderPath = DefaultLogFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshContentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private FileFolderNode? _selectedFile;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private LogFilterMode _filterMode = LogFilterMode.All;

    [ObservableProperty]
    private bool _wordWrap;

    [ObservableProperty]
    private bool _autoRefresh;

    /// <summary>Show only files with "Veyron" in the name — where errors usually land.</summary>
    [ObservableProperty]
    private bool _veyronOnly;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshContentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAllCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _fileSummary;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _totalLines;

    [ObservableProperty]
    private int _shownLines;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public LogsViewModel(IManageService manage, AxiomEnvironmentContext context, IMessenger messenger)
    {
        _manage = manage;
        _context = context;
        _messenger = messenger;

        _tailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _tailTimer.Tick += async (_, _) => await TailAsync();
    }

    partial void OnSelectedFileChanged(FileFolderNode? value)
    {
        if (value is not null)
        {
            _ = OpenFileCommand.ExecuteAsync(null);
        }
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    partial void OnFilterModeChanged(LogFilterMode value) => ApplyFilter();

    partial void OnVeyronOnlyChanged(bool value) => ApplyFileFilter();

    partial void OnAutoRefreshChanged(bool value)
    {
        if (value && SelectedFile is not null)
        {
            _tailTimer.Start();
        }
        else
        {
            _tailTimer.Stop();
        }
    }

    [RelayCommand]
    private Task InitializeAsync() => ReloadFolderAsync();

    private bool CanReloadFolder() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanReloadFolder))]
    private async Task ReloadFolderAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Listando logs en {LogFolderPath}…";

        try
        {
            var response = await _manage.GetFileFolderViewAsync(LogFolderPath);
            if (!response.Success)
            {
                StatusMessage = null;
                ErrorMessage = $"No se pudo listar {LogFolderPath}: {Shorten(response.CustomMessage)}";
                return;
            }

            _allFiles = Flatten(response.DataObject ?? [])
                .Where(n => string.Equals(n.ObjectType, "File", StringComparison.OrdinalIgnoreCase))
                .Where(n => n.Name?.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ?? false)
                .OrderByDescending(n => n.DateModified ?? DateTimeOffset.MinValue)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyFileFilter();

            StatusMessage = _allFiles.Count == 0
                ? $"No hay archivos .log en {LogFolderPath}."
                : $"{_allFiles.Count} archivo(s) .log — el más reciente arriba.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron listar los logs: {Shorten(ex.Message)}{ResponseBodyHint(ex)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenFile() => !IsBusy && SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync()
    {
        await LoadContentAsync(scrollToEnd: false, showBusy: true);
        if (AutoRefresh)
        {
            _tailTimer.Start();
        }
    }

    private bool CanRefreshContent() => !IsBusy && SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanRefreshContent))]
    private Task RefreshContentAsync() => LoadContentAsync(scrollToEnd: true, showBusy: true);

    /// <summary>Timer-driven silent reload (no spinner, jumps to the newest line).</summary>
    private async Task TailAsync()
    {
        if (IsBusy || SelectedFile is null)
        {
            return;
        }

        await LoadContentAsync(scrollToEnd: true, showBusy: false);
    }

    private async Task LoadContentAsync(bool scrollToEnd, bool showBusy)
    {
        var node = SelectedFile;
        if (node?.Path is null)
        {
            return;
        }

        if (showBusy)
        {
            IsBusy = true;
        }

        ErrorMessage = null;

        try
        {
            var response = await _manage.GetFileContentAsync(node.Path);
            if (!response.Success || response.DataObject is null)
            {
                ErrorMessage = $"No se pudo leer el log: {Shorten(response.CustomMessage)}";
                return;
            }

            var raw = response.DataObject.Content ?? string.Empty;
            var text = Base64Text.TryDecode(raw, out var decoded, out _) ? decoded : raw;

            _allLines = LogClassifier.Parse(text);

            // Count distinct entries, not lines: a stack trace is one error, so its
            // continuation lines (IsContinuation) don't add to the tally.
            ErrorCount = _allLines.Count(l => l.Severity == LogSeverity.Error && !l.IsContinuation);
            WarningCount = _allLines.Count(l => l.Severity == LogSeverity.Warning && !l.IsContinuation);
            TotalLines = _allLines.Count;

            ApplyFilter();

            var stamp = DateTime.Now.ToString("HH:mm:ss");
            FileSummary = $"{node.Name} · {TotalLines:N0} líneas · {ErrorCount} errores · {WarningCount} warnings · {stamp}";

            if (scrollToEnd)
            {
                ScrolledToEndRequested?.Invoke();
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error leyendo {node.Name}: {Shorten(ex.Message)}{ResponseBodyHint(ex)}";
        }
        finally
        {
            if (showBusy)
            {
                IsBusy = false;
            }
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText?.Trim();
        var hasSearch = !string.IsNullOrEmpty(search);

        IEnumerable<LogLine> query = _allLines;

        query = FilterMode switch
        {
            LogFilterMode.ProblemsOnly => query.Where(l => l.IsProblem),
            LogFilterMode.ErrorsOnly => query.Where(l => l.Severity == LogSeverity.Error),
            _ => query,
        };

        if (hasSearch)
        {
            query = query.Where(l => l.Text.Contains(search!, StringComparison.OrdinalIgnoreCase));
        }

        Lines.Clear();
        foreach (var line in query)
        {
            Lines.Add(line);
        }

        ShownLines = Lines.Count;
    }

    private void ApplyFileFilter()
    {
        IEnumerable<FileFolderNode> query = _allFiles;
        if (VeyronOnly)
        {
            query = query.Where(n => n.Name?.Contains("veyron", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        Files.Clear();
        foreach (var file in query)
        {
            Files.Add(file);
        }

        DeleteAllCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteSelected() => !IsBusy && SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var file = SelectedFile!;

        var confirmation = MessageBox.Show(
            $"¿Eliminar el log {file.Name}?\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _manage.DeleteFileAsync(file.Path!);
            if (result.Success)
            {
                StatusMessage = $"Eliminado: {file.Name}.";
                ClearOpenedContent();
            }
            else
            {
                ErrorMessage = $"No se pudo eliminar {file.Name}: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error eliminando {file.Name}: {Shorten(ex.Message)}{ResponseBodyHint(ex)}";
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadFolderAsync();
    }

    private bool CanDeleteAll() => !IsBusy && Files.Count > 0;

    /// <summary>Deletes every log currently listed — respects the "Solo Veyron" filter, so
    /// checking it first scopes the bulk delete to just those files.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteAll))]
    private async Task DeleteAllAsync()
    {
        var targets = Files.ToList();
        var scope = VeyronOnly ? "de Veyron" : "listados";

        var confirmation = MessageBox.Show(
            $"¿Eliminar los {targets.Count} log(s) {scope}?\n\n" +
            string.Join("\n", targets.Take(12).Select(t => $"  • {t.Name}")) +
            (targets.Count > 12 ? $"\n  … y {targets.Count - 12} más." : string.Empty) +
            "\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación masiva",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Eliminando {targets.Count} log(s)…";

        var failures = new List<string>();
        var gate = new SemaphoreSlim(4);

        try
        {
            await Task.WhenAll(targets.Select(async node =>
            {
                await gate.WaitAsync();
                try
                {
                    var result = await _manage.DeleteFileAsync(node.Path!);
                    if (!result.Success)
                    {
                        lock (failures)
                        {
                            failures.Add($"{node.Name}: {Shorten(result.CustomMessage)}");
                        }
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    lock (failures)
                    {
                        failures.Add($"{node.Name}: {Shorten(ex.Message)}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));

            if (failures.Count > 0)
            {
                ErrorMessage = $"{failures.Count} log(s) no se pudieron eliminar:\n" +
                               string.Join("\n", failures.Take(5)) +
                               (failures.Count > 5 ? $"\n… y {failures.Count - 5} más." : string.Empty);
            }

            ClearOpenedContent();
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadFolderAsync();
    }

    private void ClearOpenedContent()
    {
        SelectedFile = null;
        _allLines = [];
        Lines.Clear();
        FileSummary = null;
        TotalLines = 0;
        ShownLines = 0;
        ErrorCount = 0;
        WarningCount = 0;
    }

    [RelayCommand]
    private void CopyContent()
    {
        if (Lines.Count == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, Lines.Select(l => l.Text)));
        StatusMessage = $"{Lines.Count:N0} línea(s) copiadas al portapapeles.";
    }

    [RelayCommand]
    private void Back()
    {
        _tailTimer.Stop();
        _messenger.Send(new BackToDashboardMessage());
    }

    /// <summary>Depth-first flatten so logs in per-component subfolders are included.</summary>
    private static IEnumerable<FileFolderNode> Flatten(IEnumerable<FileFolderNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (node.Children is { Count: > 0 })
            {
                foreach (var child in Flatten(node.Children))
                {
                    yield return child;
                }
            }
        }
    }

    private static string Shorten(string? message, int maxLength = 250)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "sin detalle";
        }

        var clean = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength] + "…";
    }

    /// <summary>
    /// Appends a snippet of the raw server response when available. A "no se pudo
    /// deserializar" error only says WHERE the call failed — the response body is
    /// what actually explains WHY (a different JSON shape on that appliance, an
    /// HTML error/login page instead of JSON, an empty folder, etc).
    /// </summary>
    private static string ResponseBodyHint(Exception ex) =>
        ex is AxiomApiException { ResponseBody.Length: > 0 } apiEx
            ? $"\n\nRespuesta del servidor:\n{Shorten(apiEx.ResponseBody, 600)}"
            : string.Empty;
}
