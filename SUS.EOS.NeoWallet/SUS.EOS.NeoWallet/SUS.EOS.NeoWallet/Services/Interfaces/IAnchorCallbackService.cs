using SUS.EOS.NeoWallet.Services.Models.AnchorCallback;
using EsrRequest = SUS.EOS.EosioSigningRequest.Models.Esr;

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Anchor-compatible callback service
/// Handles external application integration and transaction signing requests
/// Compatible with Anchor wallet callback protocol
/// </summary>
public interface IAnchorCallbackService
{
    /// <summary>
    /// Handle ESR signing request from external application
    /// </summary>
    Task<AnchorCallbackResult> HandleSigningRequestAsync(string esrUri, string? password = null);

    /// <summary>
    /// Register callback handler for specific chain
    /// </summary>
    void RegisterCallbackHandler(string chainId, Func<EsrRequest, Task<bool>> handler);

    /// <summary>
    /// Send signed transaction back to requesting application
    /// </summary>
    Task<bool> SendCallbackResponseAsync(string callbackUrl, AnchorCallbackPayload payload);

    /// <summary>
    /// Handle deep link from external application (esr://, anchor://)
    /// </summary>
    Task<bool> HandleDeepLinkAsync(string deepLink);
}
