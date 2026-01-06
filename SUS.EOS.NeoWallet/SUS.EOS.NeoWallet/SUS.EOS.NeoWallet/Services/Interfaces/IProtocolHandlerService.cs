namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Service to manage protocol handler registration
/// </summary>
public partial interface IProtocolHandlerService
{
    /// <summary>
    /// Register esr:// and anchor:// protocol handlers
    /// </summary>
    void RegisterProtocolHandlers();

    /// <summary>
    /// Check if this app is the default handler for a protocol
    /// </summary>
    bool IsDefaultHandler(string protocol);

    /// <summary>
    /// Unregister protocol handlers
    /// </summary>
    void UnregisterProtocolHandlers();
}
