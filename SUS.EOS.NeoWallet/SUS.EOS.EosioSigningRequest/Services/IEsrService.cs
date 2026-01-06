using SUS.EOS.EosioSigningRequest.Models;

namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// ESR service for handling signing requests
/// </summary>
public interface IEsrService
{
    /// <summary>
    /// Parse ESR from URI
    /// </summary>
    Task<Esr> ParseRequestAsync(string uri);

    /// <summary>
    /// Sign ESR and return response (requires blockchain client for chain info and optional broadcasting)
    /// </summary>
    Task<EsrCallbackResponse> SignRequestAsync(
        Esr request,
        string privateKeyWif,
        string signer,
        string signerPermission,
        object? blockchainClient = null,
        bool broadcast = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sign and broadcast transaction
    /// </summary>
    Task<EsrCallbackResponse> SignAndBroadcastAsync(
        Esr request,
        string privateKeyWif,
        string signer,
        string signerPermission,
        object blockchainClient,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Send callback response
    /// </summary>
    Task<bool> SendCallbackAsync(Esr request, EsrCallbackResponse response);
}
