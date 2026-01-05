using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Service that tracks the currently active wallet account and network.
/// This provides a single source of truth for the active context throughout the app.
/// </summary>
public class WalletContextService : IWalletContextService
{
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    
    private WalletAccount? _activeAccount;
    private NetworkConfig? _activeNetwork;
    private bool _isInitialized;

    /// <summary>
    /// Event fired when the active account changes
    /// </summary>
    public event EventHandler<WalletAccount?>? ActiveAccountChanged;

    /// <summary>
    /// Event fired when the active network changes
    /// </summary>
    public event EventHandler<NetworkConfig?>? ActiveNetworkChanged;

    /// <summary>
    /// Event fired when the context is fully initialized
    /// </summary>
    public event EventHandler? ContextInitialized;

    public WalletContextService(IWalletStorageService storageService, INetworkService networkService)
    {
        _storageService = storageService;
        _networkService = networkService;
    }

    /// <summary>
    /// Gets the currently active wallet account
    /// </summary>
    public WalletAccount? ActiveAccount => _activeAccount;

    /// <summary>
    /// Gets the currently active network
    /// </summary>
    public NetworkConfig? ActiveNetwork => _activeNetwork;

    /// <summary>
    /// Gets whether the context has been initialized
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets whether there is an active account selected
    /// </summary>
    public bool HasActiveAccount => _activeAccount != null;

    /// <summary>
    /// Initialize the wallet context from stored preferences
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            // Load the default network
            _activeNetwork = await _networkService.GetDefaultNetworkAsync();

            // Try to load the last active account from preferences
            var lastAccountKey = Preferences.Get("LastActiveAccount", string.Empty);
            var lastChainId = Preferences.Get("LastActiveChainId", string.Empty);

            if (!string.IsNullOrEmpty(lastAccountKey) && !string.IsNullOrEmpty(lastChainId))
            {
                // Parse "account@permission" format
                var parts = lastAccountKey.Split('@');
                if (parts.Length == 2)
                {
                    var wallet = await _storageService.LoadWalletAsync();
                    if (wallet != null)
                    {
                        _activeAccount = wallet.Wallets.FirstOrDefault(w =>
                            w.Data.Account == parts[0] &&
                            w.Data.Authority == parts[1] &&
                            w.Data.ChainId == lastChainId);
                    }
                }
            }

            // If no last account but we have accounts, select the first one
            if (_activeAccount == null)
            {
                var wallet = await _storageService.LoadWalletAsync();
                if (wallet?.Wallets.Count > 0)
                {
                    // Prefer accounts on the active network
                    _activeAccount = wallet.Wallets.FirstOrDefault(w => 
                        _activeNetwork != null && w.Data.ChainId == _activeNetwork.ChainId)
                        ?? wallet.Wallets.First();
                }
            }

            _isInitialized = true;
            ContextInitialized?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WalletContext] Failed to initialize: {ex.Message}");
            _isInitialized = true; // Mark as initialized even on error
        }
    }

    /// <summary>
    /// Set the active wallet account
    /// </summary>
    public async Task SetActiveAccountAsync(WalletAccount account)
    {
        if (_activeAccount?.Data.PublicKey == account.Data.PublicKey &&
            _activeAccount?.Data.Account == account.Data.Account &&
            _activeAccount?.Data.Authority == account.Data.Authority)
        {
            return; // No change
        }

        _activeAccount = account;

        // Save to preferences
        Preferences.Set("LastActiveAccount", $"{account.Data.Account}@{account.Data.Authority}");
        Preferences.Set("LastActiveChainId", account.Data.ChainId);

        // If the account is on a different network, switch networks
        if (_activeNetwork?.ChainId != account.Data.ChainId)
        {
            var networks = await _networkService.GetNetworksAsync();
            var networkEntry = networks.FirstOrDefault(n => n.Value.ChainId == account.Data.ChainId);
            if (networkEntry.Value != null)
            {
                _activeNetwork = networkEntry.Value;
                await _networkService.SetDefaultNetworkAsync(networkEntry.Key);
                ActiveNetworkChanged?.Invoke(this, _activeNetwork);
            }
        }

        ActiveAccountChanged?.Invoke(this, _activeAccount);
    }

    /// <summary>
    /// Set the active wallet account by account details
    /// </summary>
    public async Task SetActiveAccountAsync(string account, string authority, string chainId)
    {
        var wallet = await _storageService.LoadWalletAsync();
        var walletAccount = wallet?.Wallets.FirstOrDefault(w =>
            w.Data.Account == account &&
            w.Data.Authority == authority &&
            w.Data.ChainId == chainId);

        if (walletAccount != null)
        {
            await SetActiveAccountAsync(walletAccount);
        }
    }

    /// <summary>
    /// Set the active network
    /// </summary>
    public async Task SetActiveNetworkAsync(NetworkConfig network)
    {
        if (_activeNetwork?.ChainId == network.ChainId)
        {
            return; // No change
        }

        _activeNetwork = network;

        // Find and set as default in network service
        var networks = await _networkService.GetNetworksAsync();
        var networkEntry = networks.FirstOrDefault(n => n.Value.ChainId == network.ChainId);
        if (!string.IsNullOrEmpty(networkEntry.Key))
        {
            await _networkService.SetDefaultNetworkAsync(networkEntry.Key);
        }

        ActiveNetworkChanged?.Invoke(this, _activeNetwork);

        // If the current active account is not on this network, try to find one that is
        if (_activeAccount != null && _activeAccount.Data.ChainId != network.ChainId)
        {
            var wallet = await _storageService.LoadWalletAsync();
            var accountOnNetwork = wallet?.Wallets.FirstOrDefault(w => w.Data.ChainId == network.ChainId);
            if (accountOnNetwork != null)
            {
                await SetActiveAccountAsync(accountOnNetwork);
            }
            else
            {
                // No account on this network - clear active account
                _activeAccount = null;
                Preferences.Remove("LastActiveAccount");
                Preferences.Remove("LastActiveChainId");
                ActiveAccountChanged?.Invoke(this, null);
            }
        }
    }

    /// <summary>
    /// Set the active network by network ID
    /// </summary>
    public async Task SetActiveNetworkAsync(string networkId)
    {
        var networks = await _networkService.GetNetworksAsync();
        if (networks.TryGetValue(networkId, out var network))
        {
            await SetActiveNetworkAsync(network);
        }
    }

    /// <summary>
    /// Get all accounts for the current network
    /// </summary>
    public async Task<List<WalletAccount>> GetAccountsForActiveNetworkAsync()
    {
        if (_activeNetwork == null) return new List<WalletAccount>();

        var wallet = await _storageService.LoadWalletAsync();
        return wallet?.Wallets
            .Where(w => w.Data.ChainId == _activeNetwork.ChainId)
            .ToList() ?? new List<WalletAccount>();
    }

    /// <summary>
    /// Get all accounts across all networks
    /// </summary>
    public async Task<List<WalletAccount>> GetAllAccountsAsync()
    {
        var wallet = await _storageService.LoadWalletAsync();
        return wallet?.Wallets.ToList() ?? new List<WalletAccount>();
    }

    /// <summary>
    /// Clear the active account (e.g., after wallet reset)
    /// </summary>
    public void ClearActiveAccount()
    {
        _activeAccount = null;
        Preferences.Remove("LastActiveAccount");
        Preferences.Remove("LastActiveChainId");
        ActiveAccountChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Refresh the context (e.g., after importing new accounts)
    /// </summary>
    public async Task RefreshAsync()
    {
        _isInitialized = false;
        await InitializeAsync();
    }
}
