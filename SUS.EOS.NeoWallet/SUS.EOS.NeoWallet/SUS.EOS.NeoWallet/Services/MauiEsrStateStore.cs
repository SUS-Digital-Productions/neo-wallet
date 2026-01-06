using SUS.EOS.EosioSigningRequest.Services;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// MAUI Preferences-based state store for ESR session manager
/// </summary>
public class MauiEsrStateStore : IEsrStateStore
{
    private readonly IPreferences _preferences;

    public MauiEsrStateStore(IPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        _preferences = preferences;
    }

    public string Get(string key, string defaultValue)
    {
        return _preferences.Get(key, defaultValue);
    }

    public void Set(string key, string value)
    {
        _preferences.Set(key, value);
    }

    public void Remove(string key)
    {
        _preferences.Remove(key);
    }

    public void Clear()
    {
        _preferences.Clear();
    }
}
