using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// File manager for the environment (Manage/Content endpoints): pick a
/// manageable folder, browse its tree, view/edit text file content, and
/// delete files or folders.
/// </summary>
public partial class FilesViewModel : ObservableObject
{
    private readonly IManageService _manage;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    public ObservableCollection<NameValue> Folders { get; } = [];
    public ObservableCollection<FileFolderNode> Nodes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadTreeCommand))]
    private NameValue? _selectedFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileSelected))]
    [NotifyPropertyChangedFor(nameof(IsFolderSelected))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFolderCommand))]
    private FileFolderNode? _selectedNode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    private FileContent? _openedFile;

    [ObservableProperty]
    private string _editorText = string.Empty;

    /// <summary>True when the opened file's content was Base64-decoded for display.</summary>
    [ObservableProperty]
    private bool _isDecodedFromBase64;

    /// <summary>Whether the decoded file had a leading UTF-8 BOM (restored on save).</summary>
    private bool _decodedHadBom;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadTreeCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFolderCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsFileSelected => string.Equals(SelectedNode?.ObjectType, "File", StringComparison.OrdinalIgnoreCase);

    public bool IsFolderSelected => SelectedNode is not null && !IsFileSelected;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public FilesViewModel(IManageService manage, AxiomEnvironmentContext context, IMessenger messenger)
    {
        _manage = manage;
        _context = context;
        _messenger = messenger;
    }

    partial void OnSelectedFolderChanged(NameValue? value)
    {
        if (value is not null)
        {
            _ = LoadTreeCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando carpetas gestionables…";

        try
        {
            var response = await _manage.GetManageableFoldersAsync();

            Folders.Clear();
            foreach (var folder in (response.DataObject ?? []).OrderBy(f => f.Value, StringComparer.OrdinalIgnoreCase))
            {
                Folders.Add(folder);
            }

            StatusMessage = $"{Folders.Count} carpetas gestionables.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron cargar las carpetas: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLoadTree() => !IsBusy && SelectedFolder is not null;

    [RelayCommand(CanExecute = nameof(CanLoadTree))]
    private async Task LoadTreeAsync()
    {
        var folder = SelectedFolder!;

        IsBusy = true;
        ErrorMessage = null;
        SelectedNode = null;
        OpenedFile = null;
        EditorText = string.Empty;
        StatusMessage = $"Listando {folder.Value}…";

        try
        {
            var response = await _manage.GetFileFolderViewAsync(folder.Value!);

            Nodes.Clear();
            foreach (var node in response.DataObject ?? [])
            {
                Nodes.Add(node);
            }

            StatusMessage = $"{Nodes.Count} elemento(s) en {folder.Value}.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudo listar {folder.Value}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Called from the view when the TreeView selection changes.</summary>
    public void OnTreeSelectionChanged(FileFolderNode? node) => SelectedNode = node;

    private bool CanOpenFile() => !IsBusy && IsFileSelected;

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync()
    {
        var node = SelectedNode!;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Abriendo {node.Name}…";

        try
        {
            var response = await _manage.GetFileContentAsync(node.Path!);
            if (!response.Success || response.DataObject is null)
            {
                StatusMessage = null;
                ErrorMessage = $"No se pudo leer el archivo: {Shorten(response.CustomMessage)}";
                return;
            }

            OpenedFile = response.DataObject;

            // The API returns file content Base64-encoded. Decode to text when it
            // round-trips as UTF-8; otherwise show the raw string (binary/unknown).
            var raw = response.DataObject.Content ?? string.Empty;
            if (Services.Base64Text.TryDecode(raw, out var decoded, out _decodedHadBom))
            {
                EditorText = decoded;
                IsDecodedFromBase64 = true;
                StatusMessage = $"{node.Name} ({decoded.Length:N0} caracteres, decodificado de Base64).";
            }
            else
            {
                EditorText = raw;
                IsDecodedFromBase64 = false;
                StatusMessage = $"{node.Name} ({raw.Length:N0} caracteres).";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"Error leyendo {node.Name}: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveFile() => !IsBusy && OpenedFile is not null;

    [RelayCommand(CanExecute = nameof(CanSaveFile))]
    private async Task SaveFileAsync()
    {
        var file = OpenedFile!;

        var confirmation = MessageBox.Show(
            $"¿Guardar los cambios en {file.Path}?",
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
            // Re-encode to Base64 if we decoded on open, restoring the original BOM
            // so the round-trip matches what the API expects.
            var contentToSend = IsDecodedFromBase64
                ? Services.Base64Text.Encode(EditorText, _decodedHadBom)
                : EditorText;

            var result = await _manage.SetFileContentAsync(new FileContent
            {
                Path = file.Path,
                DisplayName = file.DisplayName,
                Content = contentToSend,
                Schema = file.Schema,
                SchemaPath = file.SchemaPath,
                SchemaContent = file.SchemaContent,
            });

            if (result.Success)
            {
                StatusMessage = $"Guardado: {file.Path}.";
            }
            else
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

    private bool CanDeleteFile() => !IsBusy && IsFileSelected;

    [RelayCommand(CanExecute = nameof(CanDeleteFile))]
    private async Task DeleteFileAsync()
    {
        var node = SelectedNode!;

        var confirmation = MessageBox.Show(
            $"¿Eliminar el archivo {node.Path}?\n\nEsta acción no se puede deshacer.",
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
            var result = await _manage.DeleteFileAsync(node.Path!);
            if (result.Success)
            {
                StatusMessage = $"Eliminado: {node.Path}.";
                OpenedFile = null;
                EditorText = string.Empty;
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

        await LoadTreeAsync();
    }

    private bool CanDeleteFolder() => !IsBusy && IsFolderSelected;

    [RelayCommand(CanExecute = nameof(CanDeleteFolder))]
    private async Task DeleteFolderAsync()
    {
        var node = SelectedNode!;

        var confirmation = MessageBox.Show(
            $"¿Eliminar la carpeta {node.Path} y todo su contenido?\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación de carpeta",
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
            var result = await _manage.DeleteFolderAsync(node.Path!, deleteParentFolder: true);
            if (result.Success)
            {
                StatusMessage = $"Carpeta eliminada: {node.Path}.";
            }
            else
            {
                ErrorMessage = $"No se pudo eliminar la carpeta: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error eliminando la carpeta: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }

        await LoadTreeAsync();
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

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
