using System.Collections.Concurrent;
using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;

namespace NeoWallet.Backend.Endpoints;

public static class EsrEndpoints
{
    private static readonly ConcurrentDictionary<string, Esr> PendingRequests = new();

    public static void MapEsrEndpoints(this WebApplication app)
    {
        app.MapPost("/api/esr/parse", async (EsrParseRequest req, IEsrService esrService, CancellationToken cancellationToken) =>
        {
            _ = cancellationToken;
            var esr = await esrService.ParseRequestAsync(req.Uri);
            var requestId = Guid.NewGuid().ToString("N");
            PendingRequests[requestId] = esr;

            var actions = ExtractActionSummaries(esr);

            return Results.Ok(new EsrParseResponse(
                RequestId: requestId,
                ChainId: esr.ChainId ?? "",
                Type: esr.Payload.IsTransaction ? "transaction" : "action",
                Actions: actions
            ));
        });

        app.MapPost("/api/esr/approve", async (EsrApproveRequest req, IWalletStateService wallet, IEsrService esrService, IChainClientFactory factory, CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            if (!PendingRequests.TryRemove(req.RequestId, out var esr))
                return Results.Problem("Request not found or already handled.", statusCode: StatusCodes.Status404NotFound);

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

            System.Diagnostics.Trace.WriteLine($"[ESR] Approved request {req.RequestId}, txid={response.TransactionId}");
            return Results.Ok(new TransferResponse(
                TransactionId: response.TransactionId ?? "signed-not-broadcast",
                Broadcast: req.Broadcast
            ));
        });

        app.MapPost("/api/esr/reject", (EsrRejectRequest req) =>
        {
            PendingRequests.TryRemove(req.RequestId, out _);
            System.Diagnostics.Trace.WriteLine($"[ESR] Reject: {req.RequestId}, reason={req.Reason}");
            return Results.Ok();
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
