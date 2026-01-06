namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// Abstract state persistence for ESR session manager
/// Allows library to work without MAUI dependencies
/// </summary>
public interface IEsrStateStore
{
    /// <summary>
    /// Get value by key
    /// </summary>
    string Get(string key, string defaultValue);

    /// <summary>
    /// Set value by key
    /// </summary>
    void Set(string key, string value);

    /// <summary>
    /// Remove value by key
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Clear all stored values
    /// </summary>
    void Clear();
}
