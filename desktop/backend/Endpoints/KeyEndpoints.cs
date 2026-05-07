using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;
using SUS.EOS.Sharp.Cryptography;

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

        app.MapPost("/api/keys/lookup", async (
            LookupStoredKeyAccountsRequest req,
            IWalletStateService wallet,
            IChainClientFactory clientFactory,
            CancellationToken cancellationToken) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(req.PublicKey);
                var publicKey = req.PublicKey.Trim();

                if (!wallet.GetKeys().Any(k => k.PublicKey == publicKey))
                    return Results.Problem("Stored key not found.", statusCode: StatusCodes.Status404NotFound);

                var publicKeyBytes = EosioKey.ParsePublicKey(publicKey);
                var publicKeyK1 = publicKey.StartsWith("PUB_K1_", StringComparison.Ordinal)
                    ? publicKey
                    : EosioKey.EncodePublicKeyK1(publicKeyBytes);

                var networks = wallet.GetNetworks();
                var chainIds = req.ChainIds is { Length: > 0 }
                    ? req.ChainIds
                    : networks.Select(n => n.ChainId).ToArray();
                var results = new List<LookupChainResult>();

                foreach (var chainId in chainIds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var net = networks.FirstOrDefault(n => n.ChainId == chainId);
                    if (net is null) continue;

                    var entries = new List<LookupAccountEntry>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    try
                    {
                        using var lightApi = clientFactory.CreateLightApiClient(chainId);
                        if (lightApi is not null)
                        {
                            var keyResult = await lightApi.GetAccountsByKeyAsync(publicKey, cancellationToken);
                            foreach (var chain in keyResult.Chains.Values.Where(c =>
                                         string.Equals(c.ChainId, chainId, StringComparison.OrdinalIgnoreCase)))
                            {
                                foreach (var acct in chain.Accounts.Where(a =>
                                             a.IsKeyControlled &&
                                             a.PublicKeys.Any(pk => MatchesKey(pk, publicKey, publicKeyK1))))
                                {
                                    if (seen.Add($"{acct.AccountName}::{acct.Permission}"))
                                        entries.Add(new LookupAccountEntry(acct.AccountName, acct.Permission));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[KEYS] Light API lookup failed for {net.Name}: {ex.Message}");
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

        app.MapPost("/api/keys/import-accounts", (ImportStoredKeyAccountsRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            try
            {
                var imported = wallet.ImportAccountsByPublicKey(req.PublicKey, req.Accounts);
                return Results.Ok(imported);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    private static bool MatchesKey(string responseKey, string publicKey, string publicKeyK1)
    {
        return string.Equals(responseKey, publicKey, StringComparison.Ordinal)
            || string.Equals(responseKey, publicKeyK1, StringComparison.Ordinal);
    }
}
