using System.Text.Json;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

/// <summary>
/// Read-only chain queries: account info, table rows, currency balances.
/// Used by the Account Viewer and various dedicated forms (Stake, RAM, Vote, etc.).
/// </summary>
public static class ChainEndpoints
{
    public static void MapChainEndpoints(this WebApplication app)
    {
        // GET /api/chain/account?account=X&chainId=Y
        // Returns the raw /v1/chain/get_account JSON so the frontend can render
        // CPU/NET/RAM, permissions, voter info, refund_request, etc.
        app.MapGet("/api/chain/account", async (
            string account,
            string chainId,
            IChainClientFactory factory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(account))
                return Results.BadRequest("account is required");
            if (string.IsNullOrWhiteSpace(chainId))
                return Results.BadRequest("chainId is required");

            try
            {
                using var rpc = factory.CreateRpcClient(chainId);
                var raw = await rpc.PostJsonAsync<JsonElement>(
                    "/v1/chain/get_account",
                    new { account_name = account },
                    cancellationToken);

                return Results.Json(raw);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CHAIN] get_account failed: {ex.Message}");
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // GET /api/chain/currency-balance?account=X&chainId=Y&contract=eosio.token&symbol=WAX
        app.MapGet("/api/chain/currency-balance", async (
            string account,
            string chainId,
            string? contract,
            string? symbol,
            IChainClientFactory factory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                using var rpc = factory.CreateRpcClient(chainId);
                var raw = await rpc.PostJsonAsync<JsonElement>(
                    "/v1/chain/get_currency_balance",
                    new
                    {
                        code = string.IsNullOrWhiteSpace(contract) ? "eosio.token" : contract,
                        account = account,
                        symbol = string.IsNullOrWhiteSpace(symbol) ? null : symbol,
                    },
                    cancellationToken);

                return Results.Json(raw);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // POST /api/chain/table-rows
        // Generic table query passthrough for advanced use (vote info, msig proposals, etc.)
        app.MapPost("/api/chain/table-rows", async (
            JsonElement body,
            IChainClientFactory factory,
            CancellationToken cancellationToken) =>
        {
            if (!body.TryGetProperty("chainId", out var chainIdEl))
                return Results.BadRequest("chainId is required");

            var chainId = chainIdEl.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(chainId))
                return Results.BadRequest("chainId is required");

            try
            {
                using var rpc = factory.CreateRpcClient(chainId);

                // Forward all properties except chainId
                using var doc = JsonDocument.Parse(body.GetRawText());
                var dict = new Dictionary<string, object?>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("chainId")) continue;
                    dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                }

                var raw = await rpc.PostJsonAsync<JsonElement>(
                    "/v1/chain/get_table_rows",
                    dict,
                    cancellationToken);

                return Results.Json(raw);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }
}
