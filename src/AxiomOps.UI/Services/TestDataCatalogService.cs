using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.Services.TestData;

namespace AxiomOps.UI.Services;

/// <summary>One testdata file plus its parsed &lt;Key&gt; identity, or why parsing failed, and its Prize/Description summary (if any).</summary>
public sealed record TestDataEntry(FileFolderNode Node, TestDataKey? Key, string? ParseError, TestDataSummary Summary);

/// <summary>
/// Read-only discovery of the environment's testdata files: resolves the folder,
/// lists the files, and reads+parses each one's &lt;Key&gt; identity. Shared by any
/// tool that needs to know "what testdata exists and for which moduleId/loginName" —
/// currently the Play Repository generator (TestDataViewModel has its own inline
/// copy of this for now since it predates this extraction).
/// </summary>
public sealed class TestDataCatalogService(IManageService manage)
{
    private const string FallbackFolder = @"C:\MGS_Testdata";

    public async Task<string> ResolveFolderAsync(CancellationToken cancellationToken = default)
    {
        var folders = await manage.GetManageableFoldersAsync(cancellationToken);
        var match = (folders.DataObject ?? [])
            .FirstOrDefault(f => f.Value?.Contains("Testdata", StringComparison.OrdinalIgnoreCase) == true);
        return match?.Value ?? FallbackFolder;
    }

    public async Task<List<FileFolderNode>> ListFilesAsync(string folder, CancellationToken cancellationToken = default)
    {
        var response = await manage.GetFileFolderViewAsync(folder, cancellationToken);

        // The folder comes back as a single root node whose children are the files.
        var nodes = response.DataObject ?? [];
        return
        [
            .. nodes
                .SelectMany(n => string.Equals(n.ObjectType, "File", StringComparison.OrdinalIgnoreCase)
                    ? [n]
                    : n.Children ?? Enumerable.Empty<FileFolderNode>())
                .Where(n => string.Equals(n.ObjectType, "File", StringComparison.OrdinalIgnoreCase)),
        ];
    }

    /// <summary>Downloads and parses one file. Never throws for a bad/unparseable Key — reports it instead.</summary>
    public async Task<TestDataEntry> ReadAndParseAsync(FileFolderNode node, CancellationToken cancellationToken = default)
    {
        var response = await manage.GetFileContentAsync(node.Path!, cancellationToken);
        var raw = response.DataObject?.Content ?? string.Empty;
        var text = Base64Text.TryDecode(raw, out var decoded, out _) ? decoded : raw;

        TestDataSummary.TryParse(text, out var summary);

        return TestDataXml.TryParseKey(text, out var key) && key is not null
            ? new TestDataEntry(node, key, null, summary)
            : new TestDataEntry(node, null, "Sin <Key> válido o ilegible", summary);
    }

    /// <summary>Convenience: resolves the folder, lists it, and reads+parses every file (gated parallel).</summary>
    public async Task<List<TestDataEntry>> ListAllAsync(int parallelism = 8, CancellationToken cancellationToken = default)
    {
        var folder = await ResolveFolderAsync(cancellationToken);
        var files = await ListFilesAsync(folder, cancellationToken);

        var gate = new SemaphoreSlim(parallelism);
        var entries = await Task.WhenAll(files.Select(async node =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await ReadAndParseAsync(node, cancellationToken);
            }
            catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
            {
                return new TestDataEntry(node, null, ex.Message, TestDataSummary.Empty);
            }
            finally
            {
                gate.Release();
            }
        }));

        return [.. entries];
    }
}
