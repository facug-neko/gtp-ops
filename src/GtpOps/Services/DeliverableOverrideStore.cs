using System.IO;
using System.Text.Json;

namespace GtpOps.Services;

/// <summary>
/// Personal, per-scope set of deliverable types the user has marked as "not
/// mandatory for us" — discarded from GTP's required set. Scope = studio/provider
/// (e.g. "LinkoStudios"), so rules can differ per game family. Persisted to
/// ~/.gtpops/deliverable-overrides.json.
/// </summary>
public interface IDeliverableOverrideStore
{
    IReadOnlySet<int> GetDiscarded(string scope);
    void Discard(string scope, int deliverableTypeId);
    void Restore(string scope, int deliverableTypeId);
}

public sealed class DeliverableOverrideStore : IDeliverableOverrideStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _gate = new();

    public DeliverableOverrideStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gtpops", "deliverable-overrides.json"))
    {
    }

    public DeliverableOverrideStore(string path)
    {
        _path = path;
    }

    public IReadOnlySet<int> GetDiscarded(string scope)
    {
        lock (_gate)
        {
            var store = Load();
            return store.TryGetValue(scope, out var ids) ? new HashSet<int>(ids) : new HashSet<int>();
        }
    }

    public void Discard(string scope, int deliverableTypeId)
    {
        lock (_gate)
        {
            var store = Load();
            var set = store.TryGetValue(scope, out var ids) ? [.. ids] : new HashSet<int>();
            if (set.Add(deliverableTypeId))
            {
                store[scope] = [.. set];
                Save(store);
            }
        }
    }

    public void Restore(string scope, int deliverableTypeId)
    {
        lock (_gate)
        {
            var store = Load();
            if (store.TryGetValue(scope, out var ids) && ids.Remove(deliverableTypeId))
            {
                store[scope] = ids;
                Save(store);
            }
        }
    }

    private Dictionary<string, List<int>> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<int>>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return [];
        }
    }

    private void Save(Dictionary<string, List<int>> store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(store, WriteOptions) + "\n");
    }
}
