using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class KeyEndpoints
{
    public static void MapKeyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/keys", (IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);
            return Results.Ok(wallet.GetKeys());
        });

        app.MapPost("/api/keys", (AddKeyRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);
            try
            {
                var key = wallet.AddKey(req.PrivateKey, req.Label);
                return Results.Ok(key);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/keys/remove", (RemoveKeyRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var ok = wallet.RemoveKey(req.PublicKey);
            return ok ? Results.Ok() : Results.Problem("Key not found.", statusCode: StatusCodes.Status404NotFound);
        });
    }
}
