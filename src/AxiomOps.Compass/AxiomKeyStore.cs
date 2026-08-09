using System.Text.Json;

namespace AxiomOps.Compass;

public interface IAxiomKeyStore
{
    string? GetKey(string envInternalName);
    void SaveKey(string envInternalName, string key);
    void DeleteKey(string envInternalName);
    IReadOnlyList<string> ListKnownEnvironments();
}

/// <summary>
/// Per-environment Axiom api-key store. Reads and writes the SAME file as the
/// axiom-compass tool (`~/.axiom-compass/keys.json`, flat map env → key) so
/// keys entered in either tool are shared. Plain-text, user-scoped file —
/// mirrors the existing convention rather than inventing a second store.
/// </summary>
public sealed class AxiomKeyStore : IAxiomKeyStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _keysFile;

    public AxiomKeyStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".axiom-compass", "keys.json"))
    {
    }

    public AxiomKeyStore(string keysFile)
    {
        _keysFile = keysFile;
    }

    public string? GetKey(string envInternalName)
    {
        var store = Load();
        return store.TryGetValue(envInternalName, out var key) && !string.IsNullOrWhiteSpace(key)
            ? key.Trim()
            : null;
    }

    public void SaveKey(string envInternalName, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envInternalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var store = Load();
        store[envInternalName] = key.Trim();
        Save(store);
    }

    public void DeleteKey(string envInternalName)
    {
        var store = Load();
        if (store.Remove(envInternalName))
        {
            Save(store);
        }
    }

    public IReadOnlyList<string> ListKnownEnvironments() => [.. Load().Keys.Order()];

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_keysFile))
        {
            return [];
        }

        try
        {
            var raw = File.ReadAllText(_keysFile);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Corrupt or locked file — treat as empty, never delete (the user
            // may want to rescue it manually).
            return [];
        }
    }

    private void Save(Dictionary<string, string> store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_keysFile)!);
        File.WriteAllText(_keysFile, JsonSerializer.Serialize(store, WriteOptions) + "\n");
    }
}
