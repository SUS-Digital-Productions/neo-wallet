using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Core wallet management service interface
/// Handles wallet creation, storage, encryption, and key management
/// Inspired by Anchor wallet's architecture
/// </summary>
public interface IWalletStorageService
{
    /// <summary>
    /// Load wallet data from storage (wallet.json)
    /// </summary>
    Task<WalletData?> LoadWalletAsync();

    /// <summary>
    /// Save wallet data to storage (wallet.json)
    /// </summary>
    Task SaveWalletAsync(WalletData walletData);

    /// <summary>
    /// Create a new wallet with encrypted storage
    /// </summary>
    Task<WalletData> CreateWalletAsync(string password, string? description = null);

    /// <summary>
    /// Validate wallet password against stored hash
    /// </summary>
    Task<bool> ValidatePasswordAsync(string password);

    /// <summary>
    /// Change wallet encryption password
    /// </summary>
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);

    /// <summary>
    /// Check if wallet file exists and is valid
    /// </summary>
    Task<bool> WalletExistsAsync();

    /// <summary>
    /// Delete wallet and all associated data
    /// </summary>
    Task DeleteWalletAsync();

    /// <summary>
    /// Get wallet backup data (for export)
    /// </summary>
    Task<string> ExportWalletAsync(string password);

    /// <summary>
    /// Import wallet from backup data
    /// </summary>
    Task<WalletData> ImportWalletAsync(string backupData, string password);

    /// <summary>
    /// Lock the wallet (clear in-memory keys)
    /// </summary>
    void LockWallet();

    /// <summary>
    /// Unlock the wallet with password
    /// </summary>
    Task<bool> UnlockWalletAsync(string password);

    /// <summary>
    /// Check if wallet is currently unlocked
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Get decrypted private key from unlocked wallet
    /// </summary>
    string? GetUnlockedPrivateKey(string publicKey);

    /// <summary>
    /// Add encrypted private key to storage
    /// </summary>
    Task<bool> AddKeyToStorageAsync(string privateKey, string publicKey, string password, string? label = null);
}

/// <summary>
/// Cryptographic operations service
/// Handles encryption, decryption, and key derivation
/// </summary>
public interface ICryptographyService
{
    /// <summary>
    /// Encrypt data using password with PBKDF2 + AES-256-CBC (Anchor-compatible)
    /// </summary>
    string Encrypt(string data, string password, int iterations = 4500);

    /// <summary>
    /// Decrypt data using password
    /// </summary>
    string Decrypt(string encryptedData, string password, int iterations = 4500);

    /// <summary>
    /// Generate a secure random private key
    /// </summary>
    string GeneratePrivateKey();

    /// <summary>
    /// Derive public key from private key
    /// </summary>
    string GetPublicKey(string privateKey, string keyPrefix = "EOS");

    /// <summary>
    /// Validate private key format
    /// </summary>
    bool IsValidPrivateKey(string privateKey);

    /// <summary>
    /// Generate secure random mnemonic phrase
    /// </summary>
    string GenerateMnemonic();

    /// <summary>
    /// Derive private key from mnemonic phrase
    /// </summary>
    string DeriveKeyFromMnemonic(string mnemonic, int accountIndex = 0);
}

/// <summary>
/// Wallet account management service
/// Handles individual wallet accounts within the storage
/// </summary>
public interface IWalletAccountService
{
    /// <summary>
    /// Add a new wallet account
    /// </summary>
    Task<WalletAccount> AddAccountAsync(string account, string authority, string chainId, string privateKey, string password, WalletMode mode = WalletMode.Hot);

    /// <summary>
    /// Remove wallet account
    /// </summary>
    Task<bool> RemoveAccountAsync(string account, string authority, string chainId);

    /// <summary>
    /// Get all wallet accounts
    /// </summary>
    Task<List<WalletAccount>> GetAccountsAsync();

    /// <summary>
    /// Get wallet account by account name and authority
    /// </summary>
    Task<WalletAccount?> GetAccountAsync(string account, string authority, string chainId);

    /// <summary>
    /// Import account from private key
    /// </summary>
    Task<WalletAccount> ImportAccountAsync(string account, string authority, string chainId, string privateKey, string password, string? label = null);

    /// <summary>
    /// Export account private key (requires password)
    /// </summary>
    Task<string> ExportAccountKeyAsync(string account, string authority, string chainId, string password);

    /// <summary>
    /// Update account label or settings
    /// </summary>
    Task<bool> UpdateAccountAsync(string account, string authority, string chainId, string? label = null, WalletMode? mode = null);

    /// <summary>
    /// Get decrypted private key for account (in-memory only)
    /// </summary>
    Task<string?> GetPrivateKeyAsync(string account, string authority, string chainId, string password);

    /// <summary>
    /// Set current active account
    /// </summary>
    Task SetCurrentAccountAsync(string account, string authority, string chainId);

    /// <summary>
    /// Get current active account
    /// </summary>
    Task<WalletAccount?> GetCurrentAccountAsync();

    /// <summary>
    /// Generate a new account with random mnemonic and derived key
    /// </summary>
    Task<(WalletAccount account, string privateKey, string mnemonic)> GenerateNewAccountAsync(
        string account, string authority, string chainId, string password, string? label = null);
}

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
    /// Get predefined networks (WAX, EOS, Telos, etc.)
    /// </summary>
    Dictionary<string, NetworkConfig> GetPredefinedNetworks();

    /// <summary>
    /// Initialize wallet with predefined networks
    /// </summary>
    Task InitializePredefinedNetworksAsync();
}

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