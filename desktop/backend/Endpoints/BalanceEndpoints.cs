using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class BalanceEndpoints
{
    public static void MapBalanceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/balances", async (string account, string chainId, IChainClientFactory factory, CancellationToken cancellationToken) =>
        {
            using var lightClient = factory.CreateLightApiClient(chainId);
            if (lightClient is null)
            {
                // Fallback: query native token via RPC
                using var rpc = factory.CreateRpcClient(chainId);
                var rows = await rpc.GetCurrencyBalanceAsync("eosio.token", account, cancellationToken: cancellationToken);
                var result = rows.Select(raw =>
                {
                    var parts = raw.Split(' ', 2);
                    _ = decimal.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var num);
                    return new BalanceDto(parts.Length > 1 ? parts[1] : "?", "eosio.token", raw, num);
                }).ToList();
                return Results.Ok(result);
            }

            var balances = await lightClient.GetAccountBalancesAsync(account, cancellationToken);
            var dtos = balances
                .Select(b =>
                {
                    _ = decimal.TryParse(b.Amount, System.Globalization.CultureInfo.InvariantCulture, out var num);
                    var formatted = num.ToString($"F{b.Decimals}", System.Globalization.CultureInfo.InvariantCulture);
                    var contract = string.IsNullOrWhiteSpace(b.Contract) ? "unknown" : b.Contract;
                    return new BalanceDto(b.Currency, contract, $"{formatted} {b.Currency}", num);
                })
                .OrderByDescending(b => b.NumericAmount)
                .ThenBy(b => b.Symbol)
                .ThenBy(b => b.Contract)
                .ToList();

            return Results.Ok(dtos);
        });
    }
}
