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
    private static readonly Dictionary<string, string> RpcEndpoints = new()
    {
        { "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", "https://wax.greymass.com" },
        { "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", "https://eos.greymass.com" },
        { "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", "https://telos.greymass.com" },
    };

    public AntelopeHttpClient CreateRpcClient(string chainId)
    {
        if (!RpcEndpoints.TryGetValue(chainId, out var endpoint))
            throw new InvalidOperationException($"No RPC endpoint configured for chain {chainId[..16]}…");

        return new AntelopeHttpClient(endpoint);
    }

    public LightApiClient? CreateLightApiClient(string chainId)
    {
        return LightApiClient.ForChain(chainId);
    }
}
