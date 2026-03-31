using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        app.MapGet("/api/wallet/summary", (IWalletStateService wallet, EsrListenerService listener) =>
            Results.Ok(new WalletSummaryDto(
                ActiveNetwork: wallet.ActiveNetwork,
                ActiveAccount: wallet.ActiveAccount,
                ListenerStatus: listener.Status.ToString()
            )));

        app.MapPost("/api/wallet/create", (CreateWalletRequest req, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            if (wallet.WalletLoaded)
                return Results.Problem("Wallet already exists.", statusCode: StatusCodes.Status409Conflict);
            var ok = wallet.CreateWallet(req.Password);
            return ok ? Results.Ok(new UnlockResponse(true, tokenHolder.Token)) : Results.Problem("Failed to create wallet.");
        });

        app.MapPost("/api/wallet/unlock", (UnlockRequest req, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            var ok = wallet.Unlock(req.Password);
            return Results.Ok(new UnlockResponse(ok, ok ? tokenHolder.Token : null));
        });

        app.MapPost("/api/wallet/lock", (IWalletStateService wallet) =>
        {
            wallet.Lock();
            return Results.Ok();
        });

        app.MapGet("/api/wallet/export", (IWalletStorageService storage, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);
            if (!storage.WalletFileExists)
                return Results.Problem("No wallet file found.", statusCode: StatusCodes.Status404NotFound);

            var bytes = storage.ReadRawFile();
            if (bytes is null)
                return Results.Problem("Failed to read wallet file.", statusCode: StatusCodes.Status500InternalServerError);

            return Results.File(bytes, "application/octet-stream", "wallet.json");
        });

        app.MapPost("/api/wallet/import", (ImportWalletRequest req, IWalletStorageService storage, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            byte[] fileBytes;
            try { fileBytes = Convert.FromBase64String(req.FileBase64); }
            catch { return Results.Problem("Invalid file data.", statusCode: StatusCodes.Status400BadRequest); }

            // Write the file, then try to unlock with the given password
            storage.WriteRawFile(fileBytes);
            var data = storage.Unlock(req.Password);
            if (data is null)
            {
                return Results.Problem("Wrong password or corrupted wallet file.", statusCode: StatusCodes.Status400BadRequest);
            }

            // Wallet is now unlocked with the imported data
            wallet.Unlock(req.Password);
            return Results.Ok(new UnlockResponse(true, tokenHolder.Token));
        });

        app.MapPost("/api/accounts/private-key", (GetPrivateKeyRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var wif = wallet.GetPrivateKeyWif(req.Account, req.Authority);
            if (string.IsNullOrEmpty(wif))
                return Results.Problem("Account not found.", statusCode: StatusCodes.Status404NotFound);

            return Results.Ok(new GetPrivateKeyResponse(wif));
        });
    }
}
