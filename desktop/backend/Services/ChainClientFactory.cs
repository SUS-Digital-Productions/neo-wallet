using SUS.EOS.Sharp.Services;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Creates AntelopeHttpClient and LightApiClient instances for known chains
/// </summary>
public interface IChainClientFactory
{
    AntelopeHttpClient CreateRpcClient(string chainId);
    LightApiClient? CreateLightApiClient(string chainId);
}

public sealed class ChainClientFactory : IChainClientFactory
{
    public static readonly Dictionary<string, string[]> DefaultRpcEndpoints = new()
    {
        {
            "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
            [
                "https://wax.greymass.com",
                "https://wax.pink.gg",
                "https://api.wax.alohaeos.com"
            ]
        },
        {
            "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
            [
                "https://eos.greymass.com",
                "https://eos.api.eosnation.io",
                "https://api.eossweden.org"
            ]
        },
        {
            "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11",
            [
                "https://telos.greymass.com",
                "https://mainnet.telos.net",
                "https://telos.caleos.io"
            ]
        },
    };

    private readonly AppSettingsService _appSettings;

    public ChainClientFactory(AppSettingsService appSettings)
    {
        _appSettings = appSettings;
    }

    public AntelopeHttpClient CreateRpcClient(string chainId)
    {
        var configured = _appSettings.GetNodeOverride(chainId);
        var defaults = DefaultRpcEndpoints.TryGetValue(chainId, out var def) ? def : null;

        var endpoints = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
            endpoints.Add(configured);
        if (defaults is not null)
            endpoints.AddRange(defaults);

        endpoints = endpoints
            .Select(e => e.TrimEnd('/'))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (endpoints.Count == 0)
            throw new InvalidOperationException($"No RPC endpoint configured for chain {chainId[..16]}…");

        return new AntelopeHttpClient(endpoints);
    }

    public static IReadOnlyList<string> GetRecommendedRpcEndpoints(string chainId) =>
        DefaultRpcEndpoints.TryGetValue(chainId, out var options) ? options : [];

    public LightApiClient? CreateLightApiClient(string chainId)
    {
        return LightApiClient.ForChain(chainId);
    }
}
