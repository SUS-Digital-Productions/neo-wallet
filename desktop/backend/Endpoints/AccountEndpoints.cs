using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Services;

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

        app.MapPost("/api/accounts/lookup", async (LookupAccountsRequest req, IWalletStateService wallet, IChainClientFactory clientFactory) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            try
            {
                var key = EosioKey.FromPrivateKey(req.PrivateKey);
                var publicKey = key.PublicKey;
                var pubKeyK1 = key.PublicKeyK1;

                var networks = wallet.GetNetworks();
                var chainIds = req.ChainIds ?? networks.Select(n => n.ChainId).ToArray();
                var results = new List<LookupChainResult>();

                foreach (var chainId in chainIds)
                {
                    var net = networks.FirstOrDefault(n => n.ChainId == chainId);
                    if (net is null) continue;

                    var entries = new List<LookupAccountEntry>();
                    try
                    {
                        var lightApi = clientFactory.CreateLightApiClient(chainId);
                        if (lightApi is not null)
                        {
                            var keyResult = await lightApi.GetAccountsByKeyAsync(publicKey);
                            foreach (var chain in keyResult.Chains.Values.Where(c =>
                                         string.Equals(c.ChainId, chainId, StringComparison.OrdinalIgnoreCase)))
                            {
                                foreach (var acct in chain.Accounts.Where(a =>
                                             a.IsKeyControlled &&
                                             a.PublicKeys.Any(pk => MatchesKey(pk, publicKey, pubKeyK1))))
                                {
                                    entries.Add(new LookupAccountEntry(acct.AccountName, acct.Permission));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[ACCOUNTS] Light API lookup failed for {net.Name}: {ex.Message}");
                    }

                    results.Add(new LookupChainResult(chainId, net.Name, net.Symbol, entries.ToArray()));
                }

                return Results.Ok(new LookupAccountsResponse(publicKey, results.ToArray()));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/api/accounts/import", (ImportAccountRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            try
            {
                var imported = wallet.ImportAccounts(req.PrivateKey, req.Accounts);
                return Results.Ok(imported);
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

            var ok = wallet.RemoveAccount(req.Account, req.Authority, req.ChainId);
            return ok ? Results.Ok() : Results.Problem("Account not found.", statusCode: StatusCodes.Status404NotFound);
        });
    }

    /// <summary>
    /// Check if a public key from the Light API response matches the queried key
    /// by comparing against both legacy EOS and modern PUB_K1_ formats.
    /// </summary>
    private static bool MatchesKey(string responseKey, string legacyKey, string k1Key)
    {
        return string.Equals(responseKey, legacyKey, StringComparison.Ordinal)
            || string.Equals(responseKey, k1Key, StringComparison.Ordinal);
    }
}
