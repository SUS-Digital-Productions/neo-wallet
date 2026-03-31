using System.Text.Json;
using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;
using SUS.EOS.Sharp.Services;

namespace NeoWallet.Backend.Endpoints;

public static class EsrEndpoints
{
    public static void MapEsrEndpoints(this WebApplication app)
    {
        app.MapPost("/api/esr/parse", async (EsrParseRequest req, IEsrService esrService, EsrListenerService listener, CancellationToken cancellationToken) =>
        {
            _ = cancellationToken;
            var esr = await esrService.ParseRequestAsync(req.Uri);
            var requestId = Guid.NewGuid().ToString("N");
            listener.PendingRequests[requestId] = (esr, null);

            var actions = ExtractActionSummaries(esr);

            return Results.Ok(new EsrParseResponse(
                RequestId: requestId,
                ChainId: esr.ChainId ?? "",
                Type: esr.Payload.IsTransaction ? "transaction" : "action",
                Actions: actions
            ));
        });

        app.MapPost("/api/esr/approve", async (EsrApproveRequest req, IWalletStateService wallet, IEsrService esrService, IChainClientFactory factory, EsrListenerService listener, CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            if (!listener.PendingRequests.TryRemove(req.RequestId, out var pending))
                return Results.Problem("Request not found or already handled.", statusCode: StatusCodes.Status404NotFound);

            var (esr, relayCallback) = pending;

            var account = wallet.ActiveAccount;
            if (account is null)
                return Results.Problem("No active account.", statusCode: StatusCodes.Status400BadRequest);

            var privateKeyWif = wallet.GetPrivateKeyWif(account.Account, account.Authority);
            if (string.IsNullOrEmpty(privateKeyWif))
                return Results.Problem("No private key available.", statusCode: StatusCodes.Status400BadRequest);

            var chainId = esr.ChainId ?? wallet.ActiveNetwork?.ChainId ?? "";
            using var rpc = factory.CreateRpcClient(chainId);

            var response = await esrService.SignRequestAsync(
                esr, privateKeyWif, account.Account, account.Authority,
                blockchainClient: rpc, broadcast: req.Broadcast,
                cancellationToken: cancellationToken);

            // Inject Anchor Link session metadata so dApps can establish persistent sessions
            if (!string.IsNullOrEmpty(listener.LinkId) && !string.IsNullOrEmpty(listener.RequestPublicKey))
            {
                response.LinkChannel = $"https://cb.anchor.link/{listener.LinkId}";
                response.LinkKey = listener.RequestPublicKey;
                response.LinkName = "NeoWallet";
            }

            System.Diagnostics.Trace.WriteLine($"[ESR] Approved request {req.RequestId}, txid={response.TransactionId}");

            // Send callback to dApp so it knows the request was signed.
            // Prefer the relay callback URL (from the envelope) over the ESR's own callback.
            var callbackUrl = relayCallback ?? esr.Callback;
            if (!string.IsNullOrEmpty(callbackUrl))
            {
                esr.Callback = callbackUrl;
                try
                {
                    var callbackSent = await esrService.SendCallbackAsync(esr, response);
                    System.Diagnostics.Trace.WriteLine($"[ESR] Callback to {callbackUrl}: {(callbackSent ? "success" : "failed")}");
                }
                catch (Exception cbEx)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Callback error: {cbEx.Message}");
                }
            }

            return Results.Ok(new TransferResponse(
                TransactionId: response.TransactionId ?? "signed-not-broadcast",
                Broadcast: req.Broadcast
            ));
        });

        app.MapPost("/api/esr/reject", (EsrRejectRequest req, EsrListenerService listener) =>
        {
            listener.PendingRequests.TryRemove(req.RequestId, out _);
            System.Diagnostics.Trace.WriteLine($"[ESR] Reject: {req.RequestId}, reason={req.Reason}");
            return Results.Ok();
        });

        app.MapPost("/api/esr/sign-raw", async (
            SignRawRequest req,
            IWalletStateService wallet,
            IChainClientFactory factory,
            IAntelopeTransactionService txService,
            CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var account = wallet.ActiveAccount;
            if (account is null)
                return Results.Problem("No active account.", statusCode: StatusCodes.Status400BadRequest);

            var privateKeyWif = wallet.GetPrivateKeyWif(account.Account, account.Authority);
            if (string.IsNullOrEmpty(privateKeyWif))
                return Results.Problem("No private key available.", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                using var rpc = factory.CreateRpcClient(req.ChainId);
                var chainInfo = await rpc.GetInfoAsync(cancellationToken);

                // Parse actions from the JSON payload
                if (req.Actions.ValueKind != JsonValueKind.Array)
                    return Results.Problem("actions must be a JSON array.", statusCode: StatusCodes.Status400BadRequest);

                string? txId = null;

                foreach (var actionElem in req.Actions.EnumerateArray())
                {
                    var contract = actionElem.GetProperty("account").GetString()!;
                    var actionName = actionElem.GetProperty("name").GetString()!;
                    var data = actionElem.GetProperty("data");

                    // Deserialize data to a dictionary for the ABI serializer
                    var dataObj = JsonSerializer.Deserialize<Dictionary<string, object>>(data.GetRawText());

                    var signed = await txService.BuildAndSignWithAbiAsync(
                        rpc, chainInfo, account.Account, privateKeyWif,
                        contract, actionName, dataObj!,
                        authority: account.Authority);

                    if (req.Broadcast)
                    {
                        var pushResult = await rpc.PushTransactionAsync(signed.Transaction, cancellationToken);
                        txId = pushResult.TransactionId;
                        System.Diagnostics.Trace.WriteLine($"[ESR] Signed raw {contract}::{actionName}, txid={txId}");
                    }
                    else
                    {
                        txId = "signed-not-broadcast";
                        System.Diagnostics.Trace.WriteLine($"[ESR] Signed raw {contract}::{actionName} (not broadcast)");
                    }
                }

                return Results.Ok(new TransferResponse(
                    TransactionId: txId ?? "no-actions",
                    Broadcast: req.Broadcast
                ));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ESR] Sign-raw failed: {ex.Message}");
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // ── ESR Listener Management ─────────────────────────────────────

        app.MapGet("/api/esr/listener/status", (EsrListenerService listener) =>
            Results.Ok(new EsrListenerStatusResponse(
                Status: listener.Status.ToString(),
                LinkId: listener.LinkId,
                RequestPublicKey: listener.RequestPublicKey,
                SessionCount: listener.Sessions.Count
            )));

        app.MapPost("/api/esr/listener/connect", async (EsrListenerService listener, CancellationToken ct) =>
        {
            await listener.ConnectAsync(ct);
            return Results.Ok(new { status = listener.Status.ToString() });
        });

        app.MapPost("/api/esr/listener/disconnect", async (EsrListenerService listener) =>
        {
            await listener.DisconnectAsync();
            return Results.Ok(new { status = listener.Status.ToString() });
        });

        app.MapPost("/api/esr/listener/test", (EsrListenerService listener) =>
        {
            // Fire a synthetic signing_request event so the frontend can verify the end-to-end flow.
            // Store a fake pending request so the frontend can navigate to the approval page.
            var requestId = Guid.NewGuid().ToString("N");
            var payload = JsonSerializer.Serialize(new
            {
                type = "signing_request",
                requestId,
                isIdentity = false,
                chainId = "",
                actions = new[] { new { account = "test", name = "diagnostic" } },
                session = (object?)null,
                callbackUrl = (string?)null,
                rawPayload = (string?)null,
            });
            listener.BroadcastDiagnosticEvent("signing_request", payload);
            return Results.Ok(new { sent = true, requestId });
        });

        // Fetch a pre-parsed pending request (created by relay listener or parse endpoint).
        app.MapGet("/api/esr/pending/{requestId}", (string requestId, EsrListenerService listener) =>
        {
            if (!listener.PendingRequests.TryGetValue(requestId, out var pending))
                return Results.NotFound();

            var (esr, _) = pending;
            var actions = ExtractActionSummaries(esr);

            return Results.Ok(new EsrParseResponse(
                RequestId: requestId,
                ChainId: esr.ChainId ?? "",
                Type: esr.Payload.IsTransaction ? "transaction" : (esr.Payload.IsAction ? "action" : "identity"),
                Actions: actions
            ));
        });

        // ── WebSocket push channel ──────────────────────────────────────
        // The frontend connects to ws://localhost:5199/api/esr/ws?token=...
        // and receives JSON messages pushed by the backend whenever an ESR
        // event (signing_request, status_changed) occurs.

        app.Map("/api/esr/ws", async (HttpContext ctx, EsrListenerService listener) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await listener.HandleWebSocketAsync(ws, ctx.RequestAborted);
        });
    }

    private static List<EsrActionSummary> ExtractActionSummaries(Esr esr)
    {
        var result = new List<EsrActionSummary>();

        // Single action request
        if (esr.Payload.IsAction && esr.Payload.Action is not null)
        {
            var (account, name) = ReadActionFields(esr.Payload.Action);
            result.Add(new EsrActionSummary(account, name));
            return result;
        }

        // Transaction request — try to pull actions array
        if (esr.Payload.IsTransaction && esr.Payload.Transaction is not null)
        {
            var tx = esr.Payload.Transaction;

            // Try reflection (typed transaction object)
            var actionsProperty = tx.GetType().GetProperty("actions")
                                  ?? tx.GetType().GetProperty("Actions");
            if (actionsProperty?.GetValue(tx) is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    var (account, name) = ReadActionFields(item);
                    result.Add(new EsrActionSummary(account, name));
                }
                return result;
            }

            // Try JsonElement
            if (tx is System.Text.Json.JsonElement je && je.TryGetProperty("actions", out var arr))
            {
                foreach (var elem in arr.EnumerateArray())
                {
                    var account = elem.TryGetProperty("account", out var a) ? a.GetString() ?? "" : "";
                    var name = elem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    result.Add(new EsrActionSummary(account, name));
                }
            }
        }

        return result;
    }

    private static (string account, string name) ReadActionFields(object action)
    {
        var type = action.GetType();
        var account = type.GetProperty("account")?.GetValue(action)?.ToString()
                      ?? type.GetProperty("Account")?.GetValue(action)?.ToString()
                      ?? "";
        var name = type.GetProperty("name")?.GetValue(action)?.ToString()
                   ?? type.GetProperty("Name")?.GetValue(action)?.ToString()
                   ?? "";
        return (account, name);
    }

}
