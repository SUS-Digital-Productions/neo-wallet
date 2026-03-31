using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class NetworkEndpoints
{
    public static void MapNetworkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/networks", (IWalletStateService wallet) =>
            Results.Ok(wallet.GetNetworks()));

        app.MapPost("/api/networks/active", (SetActiveNetworkRequest req, IWalletStateService wallet) =>
        {
            try
            {
                wallet.SetActiveNetwork(req.ChainId);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }
}
