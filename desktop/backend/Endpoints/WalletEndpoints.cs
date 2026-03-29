using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        app.MapGet("/api/wallet/summary", (IWalletStateService wallet) =>
            Results.Ok(new WalletSummaryDto(
                ActiveNetwork: wallet.ActiveNetwork,
                ActiveAccount: wallet.ActiveAccount,
                ListenerStatus: "Disconnected"
            )));

        app.MapPost("/api/wallet/create", (CreateWalletRequest req, IWalletStateService wallet) =>
        {
            if (wallet.WalletLoaded)
                return Results.Problem("Wallet already exists.", statusCode: StatusCodes.Status409Conflict);
            var ok = wallet.CreateWallet(req.Password);
            return ok ? Results.Ok(new UnlockResponse(true)) : Results.Problem("Failed to create wallet.");
        });

        app.MapPost("/api/wallet/unlock", (UnlockRequest req, IWalletStateService wallet) =>
        {
            var ok = wallet.Unlock(req.Password);
            return Results.Ok(new UnlockResponse(ok));
        });

        app.MapPost("/api/wallet/lock", (IWalletStateService wallet) =>
        {
            wallet.Lock();
            return Results.Ok();
        });
    }
}
