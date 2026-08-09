using AxiomOps.Services;
using AxiomOps.Services.Models;

namespace AxiomOps.UI.Services;

/// <summary>One verified item, mirroring a row of the portal's Troubleshoot Install modal.</summary>
public sealed class GameCheckResult
{
    /// <summary>Value shown big (e.g. "101854", "x64", "LinkoStudios").</summary>
    public required string Value { get; init; }

    /// <summary>What was checked (e.g. "ModuleId", "Architecture").</summary>
    public required string Label { get; init; }

    public required bool Passed { get; init; }
}

/// <summary>Install diagnosis for one game (module/client).</summary>
public sealed class GameInstallDiagnosis
{
    public required InstalledGameRecord Game { get; init; }
    public required IReadOnlyList<GameCheckResult> DatabaseChecks { get; init; }
    public required IReadOnlyList<GameCheckResult> FileChecks { get; init; }
    public required IReadOnlyList<GameCheckResult> DependencyChecks { get; init; }

    public string? DisplayName => Game.DisplayName;
    public int ModuleId => Game.ModuleId;
    public int ClientId => Game.ClientId;
    public string? GameProvider => Game.GameProvider;

    public IEnumerable<GameCheckResult> AllChecks =>
        DatabaseChecks.Concat(FileChecks).Concat(DependencyChecks);

    public int FailedCount => AllChecks.Count(c => !c.Passed);

    public bool IsHealthy => FailedCount == 0;
}

/// <summary>
/// Recreates the portal's "Troubleshoot Install" verdict from the public API:
/// database record (InstalledGameRecords), Veyron artifacts (veyronGameRecord)
/// and DLL dependency existence (DependencyCheck).
/// </summary>
public sealed class GameInstallDiagnosticsService(IGamesService games)
{
    public async Task<List<GameInstallDiagnosis>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var fullTask = games.GetInstalledGameRecordsAsync(cancellationToken);
        var depsTask = games.GetGameDependenciesAsync(cancellationToken);
        await Task.WhenAll(fullTask, depsTask);

        var records = fullTask.Result.DataObject ?? [];
        var dependencyReports = (depsTask.Result.DataObject ?? [])
            .ToLookup(d => (d.ModuleId, d.ClientId));

        return
        [
            .. records
                .Select(g => Diagnose(g, dependencyReports[(g.ModuleId, g.ClientId)].FirstOrDefault()))
                .OrderBy(d => d.IsHealthy)
                .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.ClientId),
        ];
    }

    private static GameInstallDiagnosis Diagnose(InstalledGameRecord game, GameDependencyReport? dependencies)
    {
        List<GameCheckResult> database =
        [
            new() { Value = Fallback(game.GameProvider), Label = "Game Provider", Passed = !string.IsNullOrWhiteSpace(game.GameProvider) },
            new() { Value = game.ModuleId.ToString(), Label = "ModuleId", Passed = game.ModuleId > 0 },
            new() { Value = game.ClientId.ToString(), Label = "ClientId", Passed = game.ClientId > 0 },
            new() { Value = game.ClientTypeId?.ToString() ?? "—", Label = "ClientTypeId", Passed = game.ClientTypeId is not null },
        ];

        List<GameCheckResult> files = [];
        if (game.VeyronGameRecord is { } veyron)
        {
            files.Add(new() { Value = Fallback(veyron.Architecture), Label = "Architecture", Passed = !string.IsNullOrWhiteSpace(veyron.Architecture) });
            files.Add(new() { Value = FileName(veyron.DotVeyronGameFilePath), Label = "Archivo .veyrongame", Passed = !string.IsNullOrWhiteSpace(veyron.DotVeyronGameFilePath) });
            files.Add(new() { Value = FileName(veyron.DotSignatureFilePath), Label = "Archivo .signature", Passed = !string.IsNullOrWhiteSpace(veyron.DotSignatureFilePath) });
            files.Add(new() { Value = Fallback(veyron.VeyronPlugin), Label = "Plugin Veyron", Passed = !string.IsNullOrWhiteSpace(veyron.VeyronPlugin) });
        }
        else if (game.BanditGameRecord is { } bandit)
        {
            var serviceCount = bandit.BanditGameServiceRecords?.Count ?? 0;
            files.Add(new() { Value = $"{serviceCount} service(s)", Label = "Registro Bandit", Passed = serviceCount > 0 });
        }
        else
        {
            files.Add(new() { Value = "no encontrado", Label = "Registro de servicio (Veyron/Bandit)", Passed = false });
        }

        var versions = game.Versions?.Count ?? 0;
        files.Add(new() { Value = versions.ToString(), Label = "Versiones de contenido subidas", Passed = versions > 0 });

        List<GameCheckResult> deps = [];
        if (dependencies?.GameDllDependencies is { Count: > 0 } dlls)
        {
            foreach (var dll in Flatten(dlls))
            {
                deps.Add(new()
                {
                    Value = FileName(dll.DependentFile),
                    Label = dll.Exists ? "Dependencia presente" : "Dependencia FALTANTE",
                    Passed = dll.Exists,
                });
            }
        }
        else
        {
            deps.Add(new() { Value = "sin datos", Label = "Reporte de dependencias", Passed = false });
        }

        return new GameInstallDiagnosis
        {
            Game = game,
            DatabaseChecks = database,
            FileChecks = files,
            DependencyChecks = deps,
        };
    }

    /// <summary>Flattens the dependency tree (root DLLs + nested plugin files), guarding against cycles.</summary>
    private static IEnumerable<GameDllDependency> Flatten(IEnumerable<GameDllDependency> dependencies)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<GameDllDependency>(dependencies);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.DependentFile is { } file && !seen.Add(file))
            {
                continue;
            }

            yield return current;

            foreach (var child in current.AdditionalPluginFiles ?? [])
            {
                queue.Enqueue(child);
            }
        }
    }

    private static string FileName(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "no encontrado" : System.IO.Path.GetFileName(path);

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;
}
