using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.Sharp.Services;

namespace NeoWallet.Backend.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        app.MapPost("/api/transfers", async (TransferRequest req, IWalletStateService wallet, IChainClientFactory factory, IAntelopeTransactionService txService, CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            // TODO: Retrieve the real private key from secure wallet storage
            // For now, transactions are built + signed but require a valid key
            var privateKeyWif = wallet.GetPrivateKeyWif(req.From, req.Authority);
            if (string.IsNullOrEmpty(privateKeyWif))
                return Results.Problem("No private key available for this account.", statusCode: StatusCodes.Status400BadRequest);

            using var rpc = factory.CreateRpcClient(req.ChainId);
            var chainInfo = await rpc.GetInfoAsync(cancellationToken);

            var transferData = new
            {
                from = req.From,
                to = req.To,
                quantity = req.Quantity,
                memo = req.Memo ?? ""
            };

            var signed = await txService.BuildAndSignWithAbiAsync(
                rpc, chainInfo, req.From, privateKeyWif,
                "eosio.token", "transfer", transferData,
                authority: req.Authority);

            var pushResult = await rpc.PushTransactionAsync(signed.Transaction, cancellationToken);

            System.Diagnostics.Trace.WriteLine($"[TRANSFER] {req.From} -> {req.To} {req.Quantity} txid={pushResult.TransactionId}");
            return Results.Ok(new TransferResponse(
                TransactionId: pushResult.TransactionId,
                Broadcast: true
            ));
        });
    }
}
