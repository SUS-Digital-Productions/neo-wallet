using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.Sharp.Services;

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

        app.MapPost("/api/networks/node", (SetNetworkNodeRequest req, IWalletStateService wallet) =>
        {
            try
            {
                wallet.SetNetworkNode(req.ChainId, req.Node);
                return Results.Ok();
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/networks/test-node", async (TestNetworkNodeRequest req, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(req.ChainId))
                return Results.Problem("chainId is required.", statusCode: StatusCodes.Status400BadRequest);

            if (string.IsNullOrWhiteSpace(req.Node))
                return Results.Problem("node is required.", statusCode: StatusCodes.Status400BadRequest);

            var endpoint = req.Node.TrimEnd('/');
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return Results.Problem("Node must be a valid http/https URL.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                using var rpc = new AntelopeHttpClient(endpoint);
                var info = await rpc.GetInfoAsync(cancellationToken);
                var matchesExpectedChain = string.Equals(info.ChainId, req.ChainId, StringComparison.OrdinalIgnoreCase);

                if (!matchesExpectedChain)
                {
                    return Results.Problem(
                        $"Endpoint responded, but it belongs to a different chain ({info.ChainId}).",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Ok(new TestNetworkNodeResponse(
                    Endpoint: endpoint,
                    ChainId: info.ChainId,
                    HeadBlockNum: info.HeadBlockNum,
                    ServerVersion: info.ServerVersion,
                    MatchesExpectedChain: matchesExpectedChain
                ));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }
}
