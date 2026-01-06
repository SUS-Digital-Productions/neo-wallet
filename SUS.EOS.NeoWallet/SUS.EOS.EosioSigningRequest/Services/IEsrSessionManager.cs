using SUS.EOS.EosioSigningRequest.Models;

namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// ESR Session Manager - Anchor Link Compatible
/// Handles WebSocket connection to Anchor Link relay for receiving signing requests from dApps.
/// </summary>
public interface IEsrSessionManager
{
    /// <summary>
    /// Event raised when a signing request is received from a dApp
    /// </summary>
    event EventHandler<EsrSigningRequestEventArgs>? SigningRequestReceived;

    /// <summary>
    /// Event raised when session manager connection status changes
    /// </summary>
    event EventHandler<EsrSessionStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Current connection status
    /// </summary>
    EsrSessionStatus Status { get; }

    /// <summary>
    /// Unique link ID for this session manager
    /// </summary>
    string LinkId { get; }

    /// <summary>
    /// Public key for signing requests
    /// </summary>
    string? RequestPublicKey { get; }

    /// <summary>
    /// Active sessions (linked dApps)
    /// </summary>
    IReadOnlyList<EsrSession> Sessions { get; }

    /// <summary>
    /// Connect to Anchor Link relay
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from Anchor Link relay
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Send callback response to dApp
    /// </summary>
    Task SendCallbackAsync(EsrCallbackPayload callback);

    /// <summary>
    /// Add/update session
    /// </summary>
    Task AddSessionAsync(EsrSession session);

    /// <summary>
    /// Remove session
    /// </summary>
    Task RemoveSessionAsync(EsrSession session);

    /// <summary>
    /// Clear all sessions
    /// </summary>
    Task ClearSessionsAsync();
}
