using System.Text.Json;
using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Price feed service implementation using CoinGecko API
/// </summary>
public class PriceFeedService : IPriceFeedService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, decimal> _priceCache = new();
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    // Symbol mapping for CoinGecko
    private readonly Dictionary<string, string> _symbolMap = new()
    {
        ["WAX"] = "wax",
        ["EOS"] = "eos",
        ["TLOS"] = "telos",
        ["XPR"] = "proton",
        ["FIO"] = "fio-protocol",
        ["LIBRE"] = "libre",
        ["UX"] = "ux-network",
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum",
        ["USDT"] = "tether",
        ["USDC"] = "usd-coin",
    };

    public PriceFeedService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<decimal?> GetPriceAsync(string symbol)
    {
        // Check cache first
        if (_priceCache.ContainsKey(symbol) && DateTime.UtcNow - _lastUpdate < _cacheDuration)
        {
            return _priceCache[symbol];
        }

        try
        {
            // Get CoinGecko ID
            if (!_symbolMap.TryGetValue(symbol.ToUpperInvariant(), out var coinId))
            {
                return null; // Unknown symbol
            }

            var response = await _httpClient.GetAsync(
                $"simple/price?ids={coinId}&vs_currencies=usd"
            );
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(
                json
            );

            if (
                data != null
                && data.TryGetValue(coinId, out var prices)
                && prices.TryGetValue("usd", out var usdPrice)
            )
            {
                _priceCache[symbol] = usdPrice;
                _lastUpdate = DateTime.UtcNow;
                return usdPrice;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<Dictionary<string, decimal>> GetPricesAsync(params string[] symbols)
    {
        var result = new Dictionary<string, decimal>();

        // Check cache first
        if (DateTime.UtcNow - _lastUpdate < _cacheDuration)
        {
            foreach (var symbol in symbols)
            {
                if (_priceCache.TryGetValue(symbol, out var cachedPrice))
                {
                    result[symbol] = cachedPrice;
                }
            }

            if (result.Count == symbols.Length)
            {
                return result; // All prices in cache
            }
        }

        try
        {
            // Get CoinGecko IDs
            var coinIds = symbols
                .Select(s => _symbolMap.TryGetValue(s.ToUpperInvariant(), out var id) ? id : null)
                .Where(id => id != null)
                .ToList();

            if (!coinIds.Any())
                return result;

            var idsParam = string.Join(",", coinIds);
            var response = await _httpClient.GetAsync(
                $"simple/price?ids={idsParam}&vs_currencies=usd"
            );

            if (!response.IsSuccessStatusCode)
                return result;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(
                json
            );

            if (data != null)
            {
                foreach (var symbol in symbols)
                {
                    if (
                        _symbolMap.TryGetValue(symbol.ToUpperInvariant(), out var coinId)
                        && data.TryGetValue(coinId, out var prices)
                        && prices.TryGetValue("usd", out var usdPrice)
                    )
                    {
                        result[symbol] = usdPrice;
                        _priceCache[symbol] = usdPrice;
                    }
                }

                _lastUpdate = DateTime.UtcNow;
            }

            return result;
        }
        catch
        {
            return result;
        }
    }

    public async Task<decimal> ConvertToUsdAsync(string symbol, decimal amount)
    {
        var price = await GetPriceAsync(symbol);
        return price.HasValue ? amount * price.Value : 0;
    }
}
