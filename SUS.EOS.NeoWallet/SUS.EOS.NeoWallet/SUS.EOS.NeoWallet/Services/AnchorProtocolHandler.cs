using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Anchor protocol handler for deep link registration
/// </summary>
public static class AnchorProtocolHandler
{
    private static IAnchorCallbackService? _callbackService;

    /// <summary>
    /// Initialize protocol handler with callback service
    /// </summary>
    public static void Initialize(IAnchorCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    /// <summary>
    /// Handle incoming protocol URI
    /// </summary>
    public static async Task<bool> HandleUriAsync(string uri)
    {
        if (_callbackService == null)
            throw new InvalidOperationException("Protocol handler not initialized");

        return await _callbackService.HandleDeepLinkAsync(uri);
    }

    /// <summary>
    /// Register custom URI scheme with OS (platform-specific)
    /// </summary>
    public static void RegisterUriScheme(string scheme = "neowallet")
    {
        // Platform-specific implementation would register the URI scheme
        // Windows: Registry modification
        // macOS: Info.plist CFBundleURLTypes
        // iOS: URL Schemes in Info.plist
        // Android: Intent filters in AndroidManifest.xml
    }
}
