using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Network/blockchain management service
/// Handles different Antelope chains configuration
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Add or update network configuration
    /// </summary>
    Task AddNetworkAsync(string networkId, NetworkConfig config);

    /// <summary>
    /// Remove network configuration
    /// </summary>
    Task<bool> RemoveNetworkAsync(string networkId);

    /// <summary>
    /// Get all configured networks
    /// </summary>
    Task<Dictionary<string, NetworkConfig>> GetNetworksAsync();

    /// <summary>
    /// Get network configuration by ID
    /// </summary>
    Task<NetworkConfig?> GetNetworkAsync(string networkId);

    /// <summary>
    /// Set default network
    /// </summary>
    Task SetDefaultNetworkAsync(string networkId);

    /// <summary>
    /// Get default network
    /// </summary>
    Task<NetworkConfig?> GetDefaultNetworkAsync();

    /// <summary>
    /// Test network connectivity
    /// </summary>
    Task<bool> TestNetworkAsync(string networkId);

    /// <summary>
    /// Initialize wallet with predefined networks
    /// </summary>
    Task InitializePredefinedNetworksAsync();
}
