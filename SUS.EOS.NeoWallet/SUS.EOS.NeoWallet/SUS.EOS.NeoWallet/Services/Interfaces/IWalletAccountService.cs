using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.NeoWallet.Services.Models.WalletData;

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Wallet account management service
/// Handles individual wallet accounts within the storage
/// </summary>
public interface IWalletAccountService
{
    /// <summary>
    /// Add a new wallet account
    /// </summary>
    Task<WalletAccount> AddAccountAsync(
        string account,
        string authority,
        string chainId,
        string privateKey,
        string password,
        WalletMode mode = WalletMode.Hot
    );

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
    Task<WalletAccount> ImportAccountAsync(
        string account,
        string authority,
        string chainId,
        string privateKey,
        string password,
        string? label = null
    );

    /// <summary>
    /// Export account private key (requires password)
    /// </summary>
    Task<string> ExportAccountKeyAsync(
        string account,
        string authority,
        string chainId,
        string password
    );

    /// <summary>
    /// Update account label or settings
    /// </summary>
    Task<bool> UpdateAccountAsync(
        string account,
        string authority,
        string chainId,
        string? label = null,
        WalletMode? mode = null
    );

    /// <summary>
    /// Get decrypted private key for account (in-memory only)
    /// </summary>
    Task<string?> GetPrivateKeyAsync(
        string account,
        string authority,
        string chainId,
        string password
    );

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
        string account,
        string authority,
        string chainId,
        string password,
        string? label = null
    );
}
