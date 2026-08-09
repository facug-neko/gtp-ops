using System.IO;
using System.Text.Json;
using AxiomOps.Compass.Gtp;

namespace GtpOps.Services;

/// <summary>
/// Curated game catalog (name → canonical Portal gameId), seeded from the same
/// data the axiom-compass / axiomtools configs use. Bundled as
/// Assets/games.catalog.json; there is no clean API to list games by canonical
/// id (the games API 401s and /games/signedOff returns empty), so this is the
/// practical source, plus raw gameId entry in the UI.
/// </summary>
public interface IGameCatalog
{
    IReadOnlyList<GtpGame> Games { get; }

    /// <summary>Studio/provider for a gameId (the deliverable-rules scope), or "General" if unknown.</summary>
    string ResolveScope(int gameId);
}

public sealed class GameCatalog : IGameCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<GtpGame> Games { get; }

    public GameCatalog()
    {
        Games = Load();
    }

    public string ResolveScope(int gameId)
    {
        var game = Games.FirstOrDefault(g => g.GameId == gameId);
        return string.IsNullOrWhiteSpace(game?.Provider) ? "General" : game!.Provider!;
    }

    private static List<GtpGame> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "games.catalog.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var games = JsonSerializer.Deserialize<List<GtpGame>>(File.ReadAllText(path), JsonOptions) ?? [];
            return [.. games.OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return [];
        }
    }
}
