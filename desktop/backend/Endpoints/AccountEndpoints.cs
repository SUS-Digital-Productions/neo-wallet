using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts", (IWalletStateService wallet) =>
            Results.Ok(wallet.GetAccounts()));

        app.MapPost("/api/accounts/active", (SetActiveAccountRequest req, IWalletStateService wallet) =>
        {
            wallet.SetActiveAccount(req.Account, req.Authority, req.ChainId);
            return Results.Ok();
        });

        app.MapPost("/api/accounts/import", (ImportAccountRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            try
            {
                var dto = wallet.ImportAccount(req.PrivateKey, req.Account, req.Authority, req.Password);
                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/accounts/remove", (RemoveAccountRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var ok = wallet.RemoveAccount(req.Account, req.Authority, req.Password);
            return ok ? Results.Ok() : Results.Problem("Account not found.", statusCode: StatusCodes.Status404NotFound);
        });
    }
}
