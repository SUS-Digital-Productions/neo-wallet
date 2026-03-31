using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Background service that manages the ESR session manager lifecycle.
/// Auto-connects when the wallet is unlocked, disconnects when locked.
/// Pushes events to connected frontend clients over WebSocket.
/// </summary>
public sealed class EsrListenerService : BackgroundService
{
    private readonly IEsrSessionManager _sessionManager;
    private readonly IWalletStateService _wallet;
    private readonly ConcurrentDictionary<string, WebSocket> _wsClients = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    /// <summary>
    /// Pending ESR requests awaiting user approval (from relay or manual parse).
    /// Key = requestId, Value = (parsed Esr, optional relay callback URL).
    /// </summary>
    public ConcurrentDictionary<string, (Esr Esr, string? RelayCallback)> PendingRequests { get; } = new();

    public EsrListenerService(IEsrSessionManager sessionManager, IWalletStateService wallet)
    {
        _sessionManager = sessionManager;
        _wallet = wallet;

        _sessionManager.SigningRequestReceived += OnSigningRequestReceived;
        _sessionManager.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Current listener connection status.
    /// </summary>
    public EsrSessionStatus Status => _sessionManager.Status;

    /// <summary>
    /// Link ID for Anchor Link compatibility.
    /// </summary>
    public string LinkId => _sessionManager.LinkId;

    /// <summary>
    /// Public key for the listener.
    /// </summary>
    public string? RequestPublicKey => _sessionManager.RequestPublicKey;

    /// <summary>
    /// Active sessions.
    /// </summary>
    public IReadOnlyList<EsrSession> Sessions => _sessionManager.Sessions;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.Diagnostics.Trace.WriteLine("[ESR-LISTENER] Background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_wallet.WalletUnlocked && _sessionManager.Status == EsrSessionStatus.Disconnected)
                {
                    await ConnectAsync(stoppingToken);
                }
                else if (!_wallet.WalletUnlocked && _sessionManager.Status != EsrSessionStatus.Disconnected)
                {
                    await DisconnectAsync();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] Error in monitor loop: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_sessionManager.Status != EsrSessionStatus.Disconnected) return;
            System.Diagnostics.Trace.WriteLine("[ESR-LISTENER] Connecting to Anchor Link relay...");
            await _sessionManager.ConnectAsync(cancellationToken);
            System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] Connected. LinkId={_sessionManager.LinkId}");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectLock.WaitAsync();
        try
        {
            if (_sessionManager.Status == EsrSessionStatus.Disconnected) return;
            System.Diagnostics.Trace.WriteLine("[ESR-LISTENER] Disconnecting from relay...");
            await _sessionManager.DisconnectAsync();
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Accept a frontend WebSocket connection and keep it alive until the client disconnects.
    /// Messages from the backend are pushed; incoming frames are ignored.
    /// </summary>
    public async Task HandleWebSocketAsync(WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        _wsClients[id] = ws;
        System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] WebSocket client connected: {id}");

        // Send current status immediately
        await SendToSocketAsync(ws, new { type = "status_changed", status = Status.ToString() });

        try
        {
            // Keep the connection open by reading (and discarding) client frames
            var buf = new byte[256];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buf, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _wsClients.TryRemove(id, out _);
            System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] WebSocket client disconnected: {id}");
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
                catch { /* best effort */ }
            }
        }
    }

    private void OnSigningRequestReceived(object? sender, EsrSigningRequestEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] Signing request received: identity={e.IsIdentityRequest}");

        // Store the parsed request so the frontend can approve by requestId
        var requestId = Guid.NewGuid().ToString("N");
        if (e.Request is not null)
        {
            PendingRequests[requestId] = (e.Request, e.Callback);
        }

        // Extract action summaries for the frontend
        var actions = new List<object>();
        if (e.Request?.Payload.IsAction == true && e.Request.Payload.Action is not null)
        {
            var (account, name) = ReadActionFields(e.Request.Payload.Action);
            actions.Add(new { account, name });
        }
        else if (e.Request?.Payload.IsTransaction == true && e.Request.Payload.Transaction is not null)
        {
            var tx = e.Request.Payload.Transaction;
            var actionsProperty = tx.GetType().GetProperty("actions") ?? tx.GetType().GetProperty("Actions");
            if (actionsProperty?.GetValue(tx) is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    var (account, name) = ReadActionFields(item);
                    actions.Add(new { account, name });
                }
            }
        }

        var payload = new
        {
            type = "signing_request",
            requestId,
            isIdentity = e.IsIdentityRequest,
            chainId = e.Request?.ChainId ?? "",
            actions,
            session = e.Session is not null ? new
            {
                e.Session.Actor,
                e.Session.Permission,
                e.Session.ChainId,
                e.Session.Name
            } : null,
            callbackUrl = e.Callback,
            rawPayload = e.RawPayload,
        };

        BroadcastJson(payload);
    }

    private static (string Account, string Name) ReadActionFields(object action)
    {
        var type = action.GetType();
        var account = type.GetProperty("account")?.GetValue(action) as string
                   ?? type.GetProperty("Account")?.GetValue(action) as string
                   ?? "?";
        var name = type.GetProperty("name")?.GetValue(action) as string
                ?? type.GetProperty("Name")?.GetValue(action) as string
                ?? "?";
        return (account, name);
    }

    private void OnStatusChanged(object? sender, EsrSessionStatusEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] Status changed: {e.Status}");
        BroadcastJson(new { type = "status_changed", status = e.Status.ToString() });
    }

    /// <summary>
    /// Send a diagnostic event to all connected WebSocket clients (for testing the event pipeline).
    /// </summary>
    public void BroadcastDiagnosticEvent(string eventType, string data)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR-LISTENER] Broadcasting diagnostic event: {eventType}");
        // data is already JSON, wrap it in the envelope
        BroadcastRaw(data);
    }

    /// <summary>
    /// Send a signing_request event to all connected WebSocket clients.
    /// Used by the /api/esr/incoming endpoint (protocol handler).
    /// </summary>
    public void BroadcastSigningRequestEvent(object payload)
    {
        BroadcastJson(payload);
    }

    private void BroadcastJson(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        BroadcastRaw(json);
    }

    private void BroadcastRaw(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var (id, ws) in _wsClients)
        {
            if (ws.State != WebSocketState.Open)
            {
                _wsClients.TryRemove(id, out _);
                continue;
            }

            // Fire-and-forget; errors just remove the client
            _ = Task.Run(async () =>
            {
                try
                {
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    _wsClients.TryRemove(id, out _);
                }
            });
        }
    }

    private static async Task SendToSocketAsync(WebSocket ws, object payload)
    {
        if (ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public override void Dispose()
    {
        _sessionManager.SigningRequestReceived -= OnSigningRequestReceived;
        _sessionManager.StatusChanged -= OnStatusChanged;

        foreach (var (_, ws) in _wsClients)
        {
            try { ws.Abort(); } catch { /* best effort */ }
        }
        _wsClients.Clear();
        _connectLock.Dispose();

        base.Dispose();
    }
}
