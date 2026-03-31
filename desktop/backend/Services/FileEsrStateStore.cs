using System.Text.Json;
using SUS.EOS.EosioSigningRequest.Services;

namespace NeoWallet.Backend.Services;

/// <summary>
/// File-backed ESR state store that persists link ID, request key, and sessions across restarts.
/// Without persistence, the wallet gets a new link ID on every restart, breaking existing dApp sessions.
/// </summary>
public sealed class FileEsrStateStore : IEsrStateStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoWallet", "esr-state.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private Dictionary<string, string> _store;

    public FileEsrStateStore()
    {
        _store = Load();
    }

    public string Get(string key, string defaultValue)
    {
        lock (_lock)
        {
            return _store.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _store[key] = value;
            Save();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            _store.Remove(key);
            Save();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
            Save();
        }
    }

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var json = File.ReadAllText(StorePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data is not null)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR-STATE] Loaded {data.Count} entries from {StorePath}");
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR-STATE] Failed to load: {ex.Message}");
        }

        return new Dictionary<string, string>();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(StorePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_store, JsonOptions);
            File.WriteAllText(StorePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR-STATE] Failed to save: {ex.Message}");
        }
    }
}
