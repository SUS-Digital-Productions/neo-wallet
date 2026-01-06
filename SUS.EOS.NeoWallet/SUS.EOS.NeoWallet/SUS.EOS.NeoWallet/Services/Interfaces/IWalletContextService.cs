using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Service for tracking the currently active wallet account and network context.
/// Provides a single source of truth for the active wallet state throughout the app.
/// </summary>
public interface IWalletContextService
{
    /// <summary>
    /// Gets the currently active wallet account
    /// </summary>
    WalletAccount? ActiveAccount { get; }

    /// <summary>
    /// Gets the currently active network
    /// </summary>
    NetworkConfig? ActiveNetwork { get; }

    /// <summary>
    /// Gets whether the context has been initialized
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets whether there is an active account selected
    /// </summary>
    bool HasActiveAccount { get; }

    /// <summary>
    /// Event fired when the active account changes
    /// </summary>
    event EventHandler<WalletAccount?>? ActiveAccountChanged;

    /// <summary>
    /// Event fired when the active network changes
    /// </summary>
    event EventHandler<NetworkConfig?>? ActiveNetworkChanged;

    /// <summary>
    /// Event fired when the context is fully initialized
    /// </summary>
    event EventHandler? ContextInitialized;

    /// <summary>
    /// Initialize the wallet context from stored preferences
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Set the active wallet account
    /// </summary>
    Task SetActiveAccountAsync(WalletAccount account);

    /// <summary>
    /// Set the active wallet account by account details
    /// </summary>
    Task SetActiveAccountAsync(string account, string authority, string chainId);

    /// <summary>
    /// Set the active network
    /// </summary>
    Task SetActiveNetworkAsync(NetworkConfig network);

    /// <summary>
    /// Set the active network by network ID
    /// </summary>
    Task SetActiveNetworkAsync(string networkId);

    /// <summary>
    /// Get all accounts for the current network
    /// </summary>
    Task<List<WalletAccount>> GetAccountsForActiveNetworkAsync();

    /// <summary>
    /// Get all accounts across all networks
    /// </summary>
    Task<List<WalletAccount>> GetAllAccountsAsync();

    /// <summary>
    /// Clear the active account (e.g., after wallet reset)
    /// </summary>
    void ClearActiveAccount();

    /// <summary>
    /// Refresh the context (e.g., after importing new accounts)
    /// </summary>
    Task RefreshAsync();
}
