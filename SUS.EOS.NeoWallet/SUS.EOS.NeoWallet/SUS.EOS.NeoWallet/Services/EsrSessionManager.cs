using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.ESR;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// ESR (EOSIO Signing Request) Session Manager - Anchor Link Compatible
/// Handles WebSocket connection to Anchor Link relay for receiving signing requests from dApps.
/// Based on @greymass/anchor-link-session-manager protocol.
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
    /// Link ID used for connecting to dApps
    /// </summary>
    string? LinkId { get; }

    /// <summary>
    /// Public key used for encryption with dApps
    /// </summary>
    string? RequestPublicKey { get; }

    /// <summary>
    /// Active sessions (connected dApps)
    /// </summary>
    IReadOnlyList<EsrSession> Sessions { get; }

    /// <summary>
    /// Connect to the Anchor Link relay service
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the relay
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Add a new session (after user approves identity request)
    /// </summary>
    Task AddSessionAsync(EsrSession session);

    /// <summary>
    /// Remove a session (revoke dApp access)
    /// </summary>
    Task RemoveSessionAsync(EsrSession session);

    /// <summary>
    /// Clear all sessions
    /// </summary>
    Task ClearSessionsAsync();

    /// <summary>
    /// Send callback response after signing
    /// </summary>
    Task SendCallbackAsync(EsrCallbackPayload callback);
}

/// <summary>
/// ESR Session Manager implementation
/// </summary>
public class EsrSessionManager : IEsrSessionManager, IDisposable
{
    private const string DefaultLinkUrl = "cb.anchor.link";
    private readonly IPreferences _preferences;
    private readonly IEsrService _esrService;
    private readonly List<EsrSession> _sessions = new();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private string? _linkId;
    private string? _requestKey;
    private string? _requestPublicKey;
    private bool _disposed;

    public event EventHandler<EsrSigningRequestEventArgs>? SigningRequestReceived;
    public event EventHandler<EsrSessionStatusEventArgs>? StatusChanged;

    public EsrSessionStatus Status { get; private set; } = EsrSessionStatus.Disconnected;
    public string? LinkId => _linkId;
    public string? RequestPublicKey => _requestPublicKey;
    public IReadOnlyList<EsrSession> Sessions => _sessions.AsReadOnly();

    public EsrSessionManager(IPreferences preferences, IEsrService esrService)
    {
        _preferences = preferences;
        _esrService = esrService;
        LoadState();
    }

    private void LoadState()
    {
        _linkId = _preferences.Get("esr_link_id", string.Empty);
        _requestKey = _preferences.Get("esr_request_key", string.Empty);

        // Generate new link ID and key if not exists
        if (string.IsNullOrEmpty(_linkId))
        {
            _linkId = GenerateLinkId();
            _preferences.Set("esr_link_id", _linkId);
        }

        if (string.IsNullOrEmpty(_requestKey))
        {
            _requestKey = GenerateRequestKey();
            _preferences.Set("esr_request_key", _requestKey);
            _requestPublicKey = DerivePublicKey(_requestKey);
        }
        else
        {
            _requestPublicKey = DerivePublicKey(_requestKey);
        }

        // Load sessions
        var sessionsJson = _preferences.Get("esr_sessions", string.Empty);
        if (!string.IsNullOrEmpty(sessionsJson))
        {
            try
            {
                var sessions = JsonSerializer.Deserialize<List<EsrSession>>(sessionsJson);
                if (sessions != null)
                {
                    _sessions.AddRange(sessions);
                }
            }
            catch { }
        }
    }

    private void SaveState()
    {
        _preferences.Set("esr_link_id", _linkId ?? string.Empty);
        _preferences.Set("esr_request_key", _requestKey ?? string.Empty);
        _preferences.Set("esr_sessions", JsonSerializer.Serialize(_sessions));
    }

    private static string GenerateLinkId()
    {
        // Generate UUID v4 style link ID
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateRequestKey()
    {
        // Generate a secp256k1 private key (32 random bytes, then WIF encode)
        using var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[32];
        rng.GetBytes(keyBytes);
        return Convert.ToBase64String(keyBytes); // Simplified - real impl would use proper key generation
    }

    private static string DerivePublicKey(string privateKey)
    {
        // Simplified - real implementation would derive proper public key from private key
        // For now, just hash the private key as placeholder
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Convert.FromBase64String(privateKey));
        return "PUB_K1_" + Convert.ToBase64String(hash)[..43]; // Placeholder format
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Status == EsrSessionStatus.Connected || Status == EsrSessionStatus.Connecting)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Already connected or connecting. Status: {Status}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ESR] Initiating connection to wss://{DefaultLinkUrl}/{_linkId}");
        
        UpdateStatus(EsrSessionStatus.Connecting);
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _webSocket = new ClientWebSocket();
            var uri = new Uri($"wss://{DefaultLinkUrl}/{_linkId}");
            
            System.Diagnostics.Debug.WriteLine($"[ESR] Connecting to WebSocket...");
            await _webSocket.ConnectAsync(uri, _connectionCts.Token);
            
            System.Diagnostics.Debug.WriteLine($"[ESR] WebSocket connected! State: {_webSocket.State}");
            UpdateStatus(EsrSessionStatus.Connected);

            // Start listening for messages
            System.Diagnostics.Debug.WriteLine("[ESR] Starting message listener task...");
            _ = ListenForMessagesAsync(_connectionCts.Token);
        }
        catch (Exception ex)
        {
            UpdateStatus(EsrSessionStatus.Disconnected);
            System.Diagnostics.Debug.WriteLine($"[ESR] Connection failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        _connectionCts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            catch { }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        UpdateStatus(EsrSessionStatus.Disconnected);
    }

    private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine("[ESR] Message listener started");
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine("[ESR] Waiting for WebSocket message...");
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                System.Diagnostics.Debug.WriteLine($"[ESR] Received message: Type={result.MessageType}, Count={result.Count}, EndOfMessage={result.EndOfMessage}");

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    System.Diagnostics.Debug.WriteLine($"[ESR] WebSocket close received: {result.CloseStatus} - {result.CloseStatusDescription}");
                    await DisconnectAsync();
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        System.Diagnostics.Debug.WriteLine($"[ESR] Complete message received ({message.Length} chars): {message.Substring(0, Math.Min(200, message.Length))}...");
                        messageBuilder.Clear();
                        await ProcessMessageAsync(message);
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ESR] Message listener exited. State={_webSocket?.State}, Cancelled={cancellationToken.IsCancellationRequested}");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ESR] Message listener cancelled");
        }
        catch (WebSocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] WebSocket error: {ex.Message}");
            UpdateStatus(EsrSessionStatus.Disconnected);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Message listener error: {ex.Message}\n{ex.StackTrace}");
            UpdateStatus(EsrSessionStatus.Disconnected);
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Processing message: {message}");
            
            var envelope = JsonSerializer.Deserialize<EsrMessageEnvelope>(message);
            if (envelope == null)
            {
                System.Diagnostics.Debug.WriteLine("[ESR] Failed to deserialize envelope");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ESR] Message type: {envelope.Type}");

            switch (envelope.Type)
            {
                case "request":
                    System.Diagnostics.Debug.WriteLine("[ESR] Handling signing request...");
                    await HandleSigningRequestAsync(envelope);
                    break;

                case "identity":
                    System.Diagnostics.Debug.WriteLine("[ESR] Handling identity request...");
                    await HandleIdentityRequestAsync(envelope);
                    break;

                case "ping":
                    System.Diagnostics.Debug.WriteLine("[ESR] Handling ping...");
                    await SendPongAsync();
                    break;
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"[ESR] Unknown message type: {envelope.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Message processing error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task HandleSigningRequestAsync(EsrMessageEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            System.Diagnostics.Debug.WriteLine("[ESR] Empty payload in signing request");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsing ESR payload: {envelope.Payload.Substring(0, Math.Min(50, envelope.Payload.Length))}...");
            
            // Decrypt payload if needed (using request key)
            var esrPayload = envelope.Payload;

            // Parse ESR
            var request = await _esrService.ParseRequestAsync(esrPayload);
            
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsed request: ChainId={request.ChainId}, Callback={request.Callback}");

            // Find matching session
            var session = _sessions.FirstOrDefault(s => s.ChainId == request.ChainId);
            
            if (session != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ESR] Found matching session: {session.Actor}@{session.Permission}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ESR] No matching session found for chain {request.ChainId}");
            }

            // Raise event for UI to handle
            var args = new EsrSigningRequestEventArgs
            {
                Request = request,
                RawPayload = esrPayload,
                Session = session,
                Callback = envelope.Callback
            };

            System.Diagnostics.Debug.WriteLine($"[ESR] Raising SigningRequestReceived event. Has subscribers: {SigningRequestReceived != null}");
            SigningRequestReceived?.Invoke(this, args);
            System.Diagnostics.Debug.WriteLine("[ESR] Event raised successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Failed to handle signing request: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task HandleIdentityRequestAsync(EsrMessageEnvelope envelope)
    {
        // Identity requests are handled similarly but create/update sessions
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            System.Diagnostics.Debug.WriteLine("[ESR] Empty payload in identity request");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsing identity ESR: {envelope.Payload.Substring(0, Math.Min(50, envelope.Payload.Length))}...");
            
            var request = await _esrService.ParseRequestAsync(envelope.Payload);
            
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsed identity request: ChainId={request.ChainId}");

            var args = new EsrSigningRequestEventArgs
            {
                Request = request,
                RawPayload = envelope.Payload,
                IsIdentityRequest = true,
                Callback = envelope.Callback
            };

            System.Diagnostics.Debug.WriteLine($"[ESR] Raising SigningRequestReceived event for identity. Has subscribers: {SigningRequestReceived != null}");
            SigningRequestReceived?.Invoke(this, args);
            System.Diagnostics.Debug.WriteLine("[ESR] Identity event raised successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Failed to handle identity request: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task SendPongAsync()
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        var pong = JsonSerializer.Serialize(new { type = "pong" });
        var bytes = Encoding.UTF8.GetBytes(pong);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task SendCallbackAsync(EsrCallbackPayload callback)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        try
        {
            var message = new
            {
                type = "callback",
                payload = callback
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Failed to send callback: {ex.Message}");
        }
    }

    public async Task AddSessionAsync(EsrSession session)
    {
        // Remove existing session for same actor/permission/chain
        _sessions.RemoveAll(s => 
            s.Actor == session.Actor && 
            s.Permission == session.Permission && 
            s.ChainId == session.ChainId);

        session.Created = DateTime.UtcNow;
        session.LastUsed = DateTime.UtcNow;
        _sessions.Add(session);
        SaveState();
        await Task.CompletedTask;
    }

    public async Task RemoveSessionAsync(EsrSession session)
    {
        _sessions.RemoveAll(s =>
            s.Actor == session.Actor &&
            s.Permission == session.Permission &&
            s.ChainId == session.ChainId);
        SaveState();
        await Task.CompletedTask;
    }

    public async Task ClearSessionsAsync()
    {
        _sessions.Clear();
        SaveState();
        await Task.CompletedTask;
    }

    private void UpdateStatus(EsrSessionStatus status)
    {
        if (Status != status)
        {
            Status = status;
            StatusChanged?.Invoke(this, new EsrSessionStatusEventArgs { Status = status });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _webSocket?.Dispose();
    }
}

#region Supporting Types

/// <summary>
/// ESR Session Status
/// </summary>
public enum EsrSessionStatus
{
    Disconnected,
    Connecting,
    Connected
}

/// <summary>
/// ESR Session - represents a linked dApp
/// </summary>
public class EsrSession
{
    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = "";

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = "";

    [JsonPropertyName("permission")]
    public string Permission { get; set; } = "";

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// ESR Signing Request Event Args
/// </summary>
public class EsrSigningRequestEventArgs : EventArgs
{
    public EosioSigningRequest? Request { get; set; }
    public string? RawPayload { get; set; }
    public EsrSession? Session { get; set; }
    public bool IsIdentityRequest { get; set; }
    public string? Callback { get; set; }
}

/// <summary>
/// ESR Session Status Event Args
/// </summary>
public class EsrSessionStatusEventArgs : EventArgs
{
    public EsrSessionStatus Status { get; set; }
}

/// <summary>
/// ESR Message Envelope (from WebSocket)
/// </summary>
internal class EsrMessageEnvelope
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("callback")]
    public string? Callback { get; set; }
}

/// <summary>
/// ESR Callback Payload (sent after signing)
/// </summary>
public class EsrCallbackPayload
{
    [JsonPropertyName("sig")]
    public string? Signature { get; set; }

    [JsonPropertyName("tx")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("sa")]
    public string? SignerActor { get; set; }

    [JsonPropertyName("sp")]
    public string? SignerPermission { get; set; }

    [JsonPropertyName("bn")]
    public uint? BlockNum { get; set; }

    [JsonPropertyName("ex")]
    public string? Expiration { get; set; }

    [JsonPropertyName("link_ch")]
    public string? LinkChannel { get; set; }

    [JsonPropertyName("link_key")]
    public string? LinkKey { get; set; }

    [JsonPropertyName("link_name")]
    public string? LinkName { get; set; }
}

#endregion
