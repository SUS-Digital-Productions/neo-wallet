using System.Text.Json;
using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Services;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Transactions;

namespace NeoWallet.Backend.Endpoints;

/// <summary>
/// Generic action signing endpoint, used by the Sign Action / Utilities page
/// and all dedicated forms (Stake, RAM, Vote, PowerUp, etc.).
/// Builds a single transaction containing one or more actions, signs it
/// with the active account's key, and (optionally) broadcasts it.
/// </summary>
public static class ActionsEndpoints
{
    public static void MapActionsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/actions/sign", async (
            SignActionsRequest req,
            IWalletStateService wallet,
            IChainClientFactory factory,
            CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var account = wallet.ActiveAccount;
            if (account is null)
                return Results.Problem("No active account.", statusCode: StatusCodes.Status400BadRequest);

            if (req.Actions is null || req.Actions.Length == 0)
                return Results.Problem("At least one action is required.", statusCode: StatusCodes.Status400BadRequest);

            var chainId = string.IsNullOrWhiteSpace(req.ChainId) ? account.ChainId : req.ChainId!;
            var privateKeyWif = wallet.GetPrivateKeyWif(account.Account, account.Authority, account.ChainId);
            if (string.IsNullOrEmpty(privateKeyWif))
                return Results.Problem("No private key available for active account.", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                using var rpc = factory.CreateRpcClient(chainId);
                var chainInfo = await rpc.GetInfoAsync(cancellationToken);
                var builder = new EosioTransactionBuilder<byte[]>(chainInfo);

                // ABI cache so we don't fetch the same contract ABI twice in one tx
                var abiCache = new Dictionary<string, AbiDefinition>(StringComparer.Ordinal);

                foreach (var action in req.Actions)
                {
                    if (string.IsNullOrWhiteSpace(action.Account) || string.IsNullOrWhiteSpace(action.Name))
                        return Results.Problem("Action.account and Action.name are required.", statusCode: StatusCodes.Status400BadRequest);

                    if (!abiCache.TryGetValue(action.Account, out var abi))
                    {
                        abi = await rpc.GetAbiAsync(action.Account)
                            ?? throw new InvalidOperationException($"Could not fetch ABI for contract '{action.Account}'");
                        abiCache[action.Account] = abi;
                    }

                    var dataObj = JsonSerializer.Deserialize<Dictionary<string, object>>(action.Data.GetRawText())
                        ?? new Dictionary<string, object>();

                    var abiSerializer = new AbiSerializer(abi);
                    var binary = abiSerializer.SerializeActionData(action.Name, dataObj);

                    // Default authorization: active account@authority
                    var auth = action.Authorization is { Length: > 0 }
                        ? action.Authorization[0]
                        : new AuthorizationDto(account.Account, account.Authority);

                    builder.AddActionWithBinaryData(action.Account, action.Name, auth.Actor, auth.Permission, binary);
                }

                var transaction = builder.Build();

                var signer = new EosioSignatureProvider(privateKeyWif);
                var signature = signer.SignTransaction(chainInfo.ChainId, transaction);

                // Use packed-transaction format for push_transaction
                var serialized = EosioSerializer.SerializeTransactionWithBinaryData(transaction);
                var packedTrx = EosioSerializer.BytesToHexString(serialized);

                if (!req.Broadcast)
                {
                    System.Diagnostics.Trace.WriteLine($"[ACTIONS] Signed {req.Actions.Length} action(s) (not broadcast)");
                    return Results.Ok(new SignActionsResponse("signed-not-broadcast", false));
                }

                var pushResult = await rpc.PushTransactionAsync(new
                {
                    signatures = new[] { signature },
                    compression = 0,
                    packed_context_free_data = "",
                    packed_trx = packedTrx,
                }, cancellationToken);

                System.Diagnostics.Trace.WriteLine(
                    $"[ACTIONS] Broadcast {req.Actions.Length} action(s) txid={pushResult.TransactionId}");

                return Results.Ok(new SignActionsResponse(pushResult.TransactionId, true));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ACTIONS] Failed: {ex.Message}");
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }
}
