using SUS.EOS.NeoWallet.Services.Models.WalletData;

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
    Task<bool> AddKeyToStorageAsync(
        string privateKey,
        string publicKey,
        string password,
        string? label = null
    );
}
