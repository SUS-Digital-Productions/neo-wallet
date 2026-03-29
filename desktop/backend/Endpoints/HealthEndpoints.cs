using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (IWalletStateService wallet) =>
            Results.Ok(new HealthDto(
                Status: "ok",
                Version: "0.1.0",
                WalletLoaded: wallet.WalletLoaded,
                WalletUnlocked: wallet.WalletUnlocked
            )));
    }
}
