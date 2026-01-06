namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// In-memory implementation of ESR state store (non-persistent)
/// Useful for testing or temporary sessions
/// </summary>
public class MemoryEsrStateStore : IEsrStateStore
{
    private readonly Dictionary<string, string> _store = new();

    public string Get(string key, string defaultValue)
    {
        return _store.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public void Set(string key, string value)
    {
        _store[key] = value;
    }

    public void Remove(string key)
    {
        _store.Remove(key);
    }

    public void Clear()
    {
        _store.Clear();
    }
}
