using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.Services.TestData;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace AxiomOps.UI.ViewModels;

/// <summary>Outcome of the coherence validation for a testdata row.</summary>
public enum TestDataValidation
{
    Unknown,
    Ok,
    Duplicate,
    Invalid,
}

/// <summary>One testdata file in the environment's testdata folder.</summary>
public sealed partial class TestDataFileRow : ObservableObject
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public DateTimeOffset? DateModified { get; init; }

    /// <summary>ModuleId parsed from the "&lt;moduleId&gt;_Xnnn.testdata" naming, when present.</summary>
    public string? ModuleId { get; init; }

    /// <summary>Coherence-check result; drives the row color.</summary>
    [ObservableProperty]
    private TestDataValidation _validation = TestDataValidation.Unknown;

    /// <summary>Human-readable identity parsed from the file's &lt;Key&gt; element.</summary>
    [ObservableProperty]
    private string? _keyInfo;

    /// <summary>Tooltip listing the files this one collides with.</summary>
    [ObservableProperty]
    private string? _conflictDetail;
}

/// <summary>
/// Dedicated testdata manager: lists the environment's testdata files, shows and
/// edits their content (Base64 round-trip), deletes them, and uploads new ones.
/// Update = PATCH Manage/Content/FileContent; upload = POST Upload/TestDataFile
/// (raw multipart bytes — the path both axiom-compass and axiom-admin use).
/// </summary>
public partial class TestDataViewModel : ObservableObject
{
    private const string FallbackFolder = @"C:\MGS_Testdata";

    private readonly IManageService _manage;
    private readonly IUploadService _upload;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private string _testDataFolder = FallbackFolder;
    private List<TestDataFileRow> _allFiles = [];
    private FileContent? _openedFile;
    private bool _decodedHadBom;

    public ObservableCollection<TestDataFileRow> Files { get; } = [];

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    private bool _showOnlyConflicts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
    private TestDataFileRow? _selectedFile;

    [ObservableProperty]
    private string _editorText = string.Empty;

    [ObservableProperty]
    private bool _isDecodedFromBase64;

    /// <summary>Prize/Description shown in the QA play repository — stored as a trailing
    /// XML comment (see <see cref="TestDataSummary"/>), edited separately from the raw
    /// XML so the editor above stays clean.</summary>
    [ObservableProperty]
    private string? _prize;

    [ObservableProperty]
    private string? _description;

    /// <summary>Filename for the "new testdata" flow.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateFileCommand))]
    private string _newFileName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(UploadFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public string TestDataFolder => _testDataFolder;

    public TestDataViewModel(
        IManageService manage,
        IUploadService upload,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _manage = manage;
        _upload = upload;
        _context = context;
        _messenger = messenger;
    }

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    partial void OnShowOnlyConflictsChanged(bool value) => ApplyFilter();

    partial void OnSelectedFileChanged(TestDataFileRow? value)
    {
        if (value is not null)
        {
            _ = OpenFileCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Resolviendo carpeta de testdata…";

        try
        {
            // Resolve the testdata folder from the manageable folders (its path can
            // differ per environment), falling back to the standard location.
            var folders = await _manage.GetManageableFoldersAsync();
            var match = (folders.DataObject ?? [])
                .FirstOrDefault(f => f.Value?.Contains("Testdata", StringComparison.OrdinalIgnoreCase) == true);
            _testDataFolder = match?.Value ?? FallbackFolder;
            OnPropertyChanged(nameof(TestDataFolder));

            await LoadFilesAsync();
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudo inicializar testdata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await LoadFilesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFilesAsync()
    {
        StatusMessage = "Listando testdatas…";

        var selectedPath = SelectedFile?.Path;
        var response = await _manage.GetFileFolderViewAsync(_testDataFolder);

        // The folder comes back as a single root node whose children are the files.
        var nodes = response.DataObject ?? [];
        var fileNodes = nodes
            .SelectMany(n => string.Equals(n.ObjectType, "File", StringComparison.OrdinalIgnoreCase)
                ? [n]
                : n.Children ?? Enumerable.Empty<FileFolderNode>())
            .Where(n => string.Equals(n.ObjectType, "File", StringComparison.OrdinalIgnoreCase));

        _allFiles =
        [
            .. fileNodes
                .Select(n => new TestDataFileRow
                {
                    Name = n.Name ?? "?",
                    Path = n.Path ?? string.Empty,
                    DateModified = n.DateModified,
                    ModuleId = ParseModuleId(n.Name),
                })
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
        ];

        ApplyFilter();
        ValidateCommand.NotifyCanExecuteChanged();
        StatusMessage = $"{_allFiles.Count} testdatas en {_testDataFolder}.";

        if (selectedPath is not null)
        {
            SelectedFile = Files.FirstOrDefault(f => f.Path == selectedPath);
        }
    }

    private bool CanOpenFile() => !IsBusy && SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync()
    {
        var file = SelectedFile!;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Abriendo {file.Name}…";

        try
        {
            var response = await _manage.GetFileContentAsync(file.Path);
            if (!response.Success || response.DataObject is null)
            {
                StatusMessage = null;
                ErrorMessage = $"No se pudo leer {file.Name}: {Shorten(response.CustomMessage)}";
                return;
            }

            _openedFile = response.DataObject;
            var raw = response.DataObject.Content ?? string.Empty;

            if (Base64Text.TryDecode(raw, out var decoded, out _decodedHadBom))
            {
                EditorText = decoded;
                IsDecodedFromBase64 = true;
            }
            else
            {
                EditorText = raw;
                IsDecodedFromBase64 = false;
            }

            // The Prize/Description live in a trailing comment, outside <Test> — pull
            // them into their own fields and keep the XML editor showing just the payload.
            TestDataSummary.TryParse(EditorText, out var summary);
            Prize = summary.Prize;
            Description = summary.Description;
            EditorText = TestDataSummary.Strip(EditorText);

            StatusMessage = $"{file.Name} ({EditorText.Length:N0} caracteres).";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"Error leyendo {file.Name}: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveFile() => !IsBusy && SelectedFile is not null && _openedFile is not null;

    [RelayCommand(CanExecute = nameof(CanSaveFile))]
    private async Task SaveFileAsync()
    {
        var file = SelectedFile!;

        var confirmation = MessageBox.Show(
            $"¿Guardar los cambios en {file.Name}?",
            "Confirmar guardado",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var finalXml = new TestDataSummary(Prize, Description).ApplyTo(EditorText);
            var contentToSend = IsDecodedFromBase64
                ? Base64Text.Encode(finalXml, _decodedHadBom)
                : finalXml;

            var result = await _manage.SetFileContentAsync(new FileContent
            {
                Path = _openedFile!.Path,
                DisplayName = _openedFile.DisplayName,
                Content = contentToSend,
                Schema = _openedFile.Schema,
                SchemaPath = _openedFile.SchemaPath,
                SchemaContent = _openedFile.SchemaContent,
            });

            StatusMessage = result.Success ? $"Guardado: {file.Name}." : null;
            if (!result.Success)
            {
                ErrorMessage = $"El server no aceptó el guardado: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error guardando: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDeleteFile() => !IsBusy && SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteFile))]
    private async Task DeleteFileAsync()
    {
        var file = SelectedFile!;

        var confirmation = MessageBox.Show(
            $"¿Eliminar el testdata {file.Name}?\n\nEsta acción no se puede deshacer.",
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
            var result = await _manage.DeleteFileAsync(file.Path);
            if (result.Success)
            {
                StatusMessage = $"Eliminado: {file.Name}.";
                _openedFile = null;
                EditorText = string.Empty;
                IsDecodedFromBase64 = false;
                Prize = null;
                Description = null;
            }
            else
            {
                ErrorMessage = $"No se pudo eliminar: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error eliminando: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }

        await LoadFilesAsync();
    }

    private bool CanUpload() => !IsBusy;

    /// <summary>Uploads one or more testdata files from disk via POST /Upload/TestDataFile.</summary>
    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí testdata(s) para subir",
            Filter = "Testdata (*.testdata)|*.testdata|Todos los archivos (*.*)|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        var ok = 0;
        var failures = new List<string>();

        try
        {
            foreach (var path in dialog.FileNames)
            {
                var name = Path.GetFileName(path);
                StatusMessage = $"Subiendo {name}…";
                try
                {
                    await using var stream = File.OpenRead(path);
                    var result = await _upload.UploadTestDataAsync(stream, name);
                    if (result.Success)
                    {
                        ok++;
                    }
                    else
                    {
                        failures.Add($"{name}: {Shorten(result.CustomMessage)}");
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException or IOException)
                {
                    failures.Add($"{name}: {Shorten(ex.Message)}");
                }
            }

            StatusMessage = $"Subida: {ok} OK, {failures.Count} fallidas.";
            if (failures.Count > 0)
            {
                ErrorMessage = string.Join("\n", failures.Take(4));
            }
        }
        finally
        {
            IsBusy = false;
        }

        await LoadFilesAsync();
    }

    private bool CanCreate() => !IsBusy && !string.IsNullOrWhiteSpace(NewFileName);

    /// <summary>Creates a new testdata from the editor content (uploaded as raw bytes with BOM).</summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateFileAsync()
    {
        var name = NewFileName.Trim();
        if (!name.Contains('.'))
        {
            name += ".testdata";
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Creando {name}…";

        try
        {
            var finalXml = new TestDataSummary(Prize, Description).ApplyTo(EditorText);
            var bytes = Base64Text.ToUtf8Bytes(finalXml, withBom: true);
            using var stream = new MemoryStream(bytes);

            var result = await _upload.UploadTestDataAsync(stream, name);
            if (result.Success)
            {
                StatusMessage = $"Creado: {name}.";
                NewFileName = string.Empty;
                await LoadFilesAsync();
                SelectedFile = Files.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                ErrorMessage = $"No se pudo crear: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error creando: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanValidate() => !IsBusy && _allFiles.Count > 0;

    /// <summary>
    /// Reads every testdata, parses its &lt;Key&gt; (moduleID [+ clientId] + loginName)
    /// and flags in red the ones sharing the same identity. Files whose Key can't
    /// be parsed are flagged as invalid (amber). The uniqueness key comes from the
    /// file CONTENT, not the filename.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task ValidateAsync()
    {
        var rows = _allFiles.ToList();

        IsBusy = true;
        ErrorMessage = null;

        var keys = new ConcurrentDictionary<TestDataFileRow, string>();
        var invalid = new ConcurrentBag<TestDataFileRow>();
        var display = new ConcurrentDictionary<TestDataFileRow, string>();
        var gate = new SemaphoreSlim(8);
        var done = 0;
        var progress = new Progress<int>(n => StatusMessage = $"Validando coherencia… {n}/{rows.Count}");

        try
        {
            await Task.WhenAll(rows.Select(async row =>
            {
                await gate.WaitAsync();
                try
                {
                    var response = await _manage.GetFileContentAsync(row.Path);
                    var raw = response.DataObject?.Content ?? string.Empty;
                    var text = Base64Text.TryDecode(raw, out var decoded, out _) ? decoded : raw;

                    if (TestDataXml.TryParseKey(text, out var parsed) && parsed is not null)
                    {
                        keys[row] = parsed.UniquenessKey;
                        display[row] = parsed.ToString();
                    }
                    else
                    {
                        invalid.Add(row);
                        display[row] = "(sin <Key> válido)";
                    }
                }
                catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
                {
                    invalid.Add(row);
                    display[row] = "(no se pudo leer)";
                }
                finally
                {
                    gate.Release();
                    ((IProgress<int>)progress).Report(Interlocked.Increment(ref done));
                }
            }));

            // Apply results on the UI thread (we're back on it after the await).
            var duplicates = keys
                .GroupBy(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            var conflictRows = new HashSet<TestDataFileRow>();
            foreach (var group in duplicates)
            {
                var members = group.ToList();
                foreach (var row in members)
                {
                    conflictRows.Add(row);
                    row.Validation = TestDataValidation.Duplicate;
                    row.ConflictDetail = "Misma identidad que: " +
                        string.Join(", ", members.Where(r => r != row).Select(r => r.Name));
                }
            }

            var invalidSet = invalid.ToHashSet();
            foreach (var row in rows)
            {
                row.KeyInfo = display.GetValueOrDefault(row);

                if (conflictRows.Contains(row))
                {
                    continue; // already marked Duplicate
                }

                if (invalidSet.Contains(row))
                {
                    row.Validation = TestDataValidation.Invalid;
                    row.ConflictDetail = "No se pudo determinar la identidad (Key ausente o ilegible).";
                }
                else
                {
                    row.Validation = TestDataValidation.Ok;
                    row.ConflictDetail = null;
                }
            }

            ApplyFilter();

            var conflictFiles = conflictRows.Count;
            var summary = conflictFiles > 0
                ? $"⚠ {conflictFiles} testdata(s) duplicados en {duplicates.Count} grupo(s)"
                : "✔ Sin duplicados";
            if (invalidSet.Count > 0)
            {
                summary += $" · {invalidSet.Count} sin Key válido";
            }
            StatusMessage = $"{summary} (de {rows.Count} analizados).";
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

        var filtered = _allFiles
            .Where(f => !ShowOnlyConflicts || f.Validation is TestDataValidation.Duplicate or TestDataValidation.Invalid)
            .Where(f => string.IsNullOrEmpty(filter)
                || f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (f.ModuleId?.Contains(filter, StringComparison.Ordinal) ?? false));

        Files.Clear();
        foreach (var file in filtered)
        {
            Files.Add(file);
        }
    }

    private static string? ParseModuleId(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var underscore = fileName.IndexOf('_');
        if (underscore <= 0)
        {
            return null;
        }

        var prefix = fileName[..underscore];
        return prefix.All(char.IsDigit) ? prefix : null;
    }

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
