using SUS.EOS.NeoWallet.Repositories.Interfaces;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Network/blockchain configuration management service
/// </summary>
public class NetworkService : INetworkService
{
    private readonly IWalletStorageService _storageService;
    private readonly INetworkRepository _networkRepository;

    public NetworkService(
        IWalletStorageService storageService,
        INetworkRepository networkRepository
    )
    {
        _storageService = storageService;
        _networkRepository = networkRepository;
    }

    /// <summary>
    /// Add or update network configuration
    /// </summary>
    public async Task AddNetworkAsync(string networkId, NetworkConfig config)
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            throw new InvalidOperationException("No wallet loaded");

        wallet.Networks[networkId] = config;
        await _storageService.SaveWalletAsync(wallet);
    }

    /// <summary>
    /// Remove network configuration
    /// </summary>
    public async Task<bool> RemoveNetworkAsync(string networkId)
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            return false;

        var removed = wallet.Networks.Remove(networkId);
        if (removed)
        {
            await _storageService.SaveWalletAsync(wallet);
        }
        return removed;
    }

    /// <summary>
    /// Get all configured networks
    /// </summary>
    public async Task<Dictionary<string, NetworkConfig>> GetNetworksAsync()
    {
        var wallet = await _storageService.LoadWalletAsync();
        return wallet?.Networks ?? new Dictionary<string, NetworkConfig>();
    }

    /// <summary>
    /// Get network configuration by ID
    /// </summary>
    public async Task<NetworkConfig?> GetNetworkAsync(string networkId)
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            return null;

        return wallet.Networks.TryGetValue(networkId, out var config) ? config : null;
    }

    /// <summary>
    /// Set default network
    /// </summary>
    public async Task SetDefaultNetworkAsync(string networkId)
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            throw new InvalidOperationException("No wallet loaded");

        if (!wallet.Networks.ContainsKey(networkId))
            throw new ArgumentException($"Network '{networkId}' not found", nameof(networkId));

        wallet.Settings.DefaultNetwork = networkId;
        await _storageService.SaveWalletAsync(wallet);
    }

    /// <summary>
    /// Get default network
    /// </summary>
    public async Task<NetworkConfig?> GetDefaultNetworkAsync()
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet?.Settings.DefaultNetwork == null)
            return null;

        return await GetNetworkAsync(wallet.Settings.DefaultNetwork);
    }

    /// <summary>
    /// Test network connectivity
    /// </summary>
    public async Task<bool> TestNetworkAsync(string networkId)
    {
        try
        {
            var network = await GetNetworkAsync(networkId);
            if (network == null)
                return false;

            using var client = new AntelopeHttpClient(network.HttpEndpoint);
            var info = await client.GetInfoAsync();

            // Verify chain ID matches
            return info.ChainId == network.ChainId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Initialize wallet with predefined networks
    /// </summary>
    public async Task InitializePredefinedNetworksAsync()
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            return;

        var predefined = _networkRepository.GetPredefinedNetworks();
        var updated = false;

        foreach (var (networkId, config) in predefined)
        {
            // Only add if not already present
            if (!wallet.Networks.ContainsKey(networkId))
            {
                wallet.Networks[networkId] = config;
                updated = true;
            }
        }

        if (updated)
        {
            await _storageService.SaveWalletAsync(wallet);
        }
    }
}
