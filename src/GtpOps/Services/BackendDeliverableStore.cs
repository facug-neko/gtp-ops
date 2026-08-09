using System.IO;
using System.Text.Json;

namespace GtpOps.Services;

/// <summary>
/// Personal set of deliverable types the user (a backend dev) owns. Global —
/// backend responsibilities are the same across studios. Persisted to
/// ~/.gtpops/backend-deliverables.json (a flat list of deliverableTypeId).
/// </summary>
public interface IBackendDeliverableStore
{
    IReadOnlySet<int> GetBackendTypeIds();
    void Mark(int deliverableTypeId);
    void Unmark(int deliverableTypeId);
}

public sealed class BackendDeliverableStore : IBackendDeliverableStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _gate = new();

    public BackendDeliverableStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gtpops", "backend-deliverables.json"))
    {
    }

    public BackendDeliverableStore(string path)
    {
        _path = path;
    }

    public IReadOnlySet<int> GetBackendTypeIds()
    {
        lock (_gate)
        {
            return new HashSet<int>(Load());
        }
    }

    public void Mark(int deliverableTypeId)
    {
        lock (_gate)
        {
            var set = new HashSet<int>(Load());
            if (set.Add(deliverableTypeId))
            {
                Save(set);
            }
        }
    }

    public void Unmark(int deliverableTypeId)
    {
        lock (_gate)
        {
            var set = new HashSet<int>(Load());
            if (set.Remove(deliverableTypeId))
            {
                Save(set);
            }
        }
    }

    private List<int> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return [];
        }
    }

    private void Save(IEnumerable<int> ids)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(ids.Order().ToList(), WriteOptions) + "\n");
    }
}
