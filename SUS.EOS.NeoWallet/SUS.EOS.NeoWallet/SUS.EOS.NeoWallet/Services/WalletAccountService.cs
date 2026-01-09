using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.NeoWallet.Services.Models.WalletData;
using SUS.EOS.Sharp.Cryptography;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Wallet account management service implementation
/// Handles wallet accounts, import/export, and current account tracking
/// </summary>
public class WalletAccountService : IWalletAccountService
{
    private readonly IWalletStorageService _storageService;
    private readonly ICryptographyService _cryptographyService;
    private WalletAccount? _currentAccount;

    public WalletAccountService(
        IWalletStorageService storageService,
        ICryptographyService cryptographyService
    )
    {
        _storageService = storageService;
        _cryptographyService = cryptographyService;
    }

    /// <summary>
    /// Add a new wallet account
    /// </summary>
    public async Task<WalletAccount> AddAccountAsync(
        string account,
        string authority,
        string chainId,
        string privateKey,
        string password,
        WalletMode mode = WalletMode.Hot
    )
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            throw new InvalidOperationException("No wallet loaded. Create a wallet first.");

        // Get public key from private key
        var eosioKey = EosioKey.FromWif(privateKey);
        var publicKey = eosioKey.PublicKey;

        // Add private key to encrypted storage
        await _storageService.AddKeyToStorageAsync(privateKey, publicKey, password);

        // Create wallet account entry
        var walletAccount = new WalletAccount
        {
            Schema = "neowallet.v1.wallet",
            Data = new WalletAccountData
            {
                Account = account,
                Authority = authority,
                ChainId = chainId,
                PublicKey = publicKey,
                Mode = mode,
                Type = WalletType.Key,
                Created = DateTime.UtcNow,
            },
        };

        // Add to wallet
        wallet.Wallets.Add(walletAccount);
        await _storageService.SaveWalletAsync(wallet);

        return walletAccount;
    }

    /// <summary>
    /// Remove wallet account
    /// </summary>
    public async Task<bool> RemoveAccountAsync(string account, string authority, string chainId)
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            return false;

        var accountToRemove = wallet.Wallets.FirstOrDefault(w =>
            w.Data.Account == account && w.Data.Authority == authority && w.Data.ChainId == chainId
        );

        if (accountToRemove == null)
            return false;

        wallet.Wallets.Remove(accountToRemove);
        await _storageService.SaveWalletAsync(wallet);

        // Clear current account if removed
        if (
            _currentAccount?.Data.Account == account
            && _currentAccount?.Data.Authority == authority
            && _currentAccount?.Data.ChainId == chainId
        )
        {
            _currentAccount = null;
        }

        return true;
    }

    /// <summary>
    /// Get all wallet accounts
    /// </summary>
    public async Task<List<WalletAccount>> GetAccountsAsync()
    {
        var wallet = await _storageService.LoadWalletAsync();
        return wallet?.Wallets ?? new List<WalletAccount>();
    }

    /// <summary>
    /// Get wallet account by account name and authority
    /// </summary>
    public async Task<WalletAccount?> GetAccountAsync(
        string account,
        string authority,
        string chainId
    )
    {
        var wallet = await _storageService.LoadWalletAsync();
        return wallet?.Wallets.FirstOrDefault(w =>
            w.Data.Account == account && w.Data.Authority == authority && w.Data.ChainId == chainId
        );
    }

    /// <summary>
    /// Import account from private key
    /// </summary>
    public async Task<WalletAccount> ImportAccountAsync(
        string account,
        string authority,
        string chainId,
        string privateKey,
        string password,
        string? label = null
    )
    {
        // Validate private key format
        if (!_cryptographyService.IsValidPrivateKey(privateKey))
            throw new ArgumentException("Invalid private key format", nameof(privateKey));

        // Add the account
        var walletAccount = await AddAccountAsync(
            account,
            authority,
            chainId,
            privateKey,
            password,
            WalletMode.Hot
        );

        // Set label if provided
        if (!string.IsNullOrEmpty(label))
        {
            walletAccount.Data.Label = label;
            var wallet = await _storageService.LoadWalletAsync();
            if (wallet != null)
            {
                await _storageService.SaveWalletAsync(wallet);
            }
        }

        return walletAccount;
    }

    /// <summary>
    /// Export account private key (requires password)
    /// </summary>
    public async Task<string> ExportAccountKeyAsync(
        string account,
        string authority,
        string chainId,
        string password
    )
    {
        // Validate password
        if (!await _storageService.ValidatePasswordAsync(password))
            throw new UnauthorizedAccessException("Invalid password");

        // Get account
        var walletAccount = await GetAccountAsync(account, authority, chainId);
        if (walletAccount == null)
            throw new InvalidOperationException("Account not found");

        // Unlock wallet to access keys
        if (!await _storageService.UnlockWalletAsync(password))
            throw new UnauthorizedAccessException("Failed to unlock wallet");

        // Get private key from storage
        var privateKey = _storageService.GetUnlockedPrivateKey(walletAccount.Data.PublicKey);
        if (privateKey == null)
            throw new InvalidOperationException("Private key not found in storage");

        return privateKey;
    }

    /// <summary>
    /// Update account label or settings
    /// </summary>
    public async Task<bool> UpdateAccountAsync(
        string account,
        string authority,
        string chainId,
        string? label = null,
        WalletMode? mode = null
    )
    {
        var wallet = await _storageService.LoadWalletAsync();
        if (wallet == null)
            return false;

        var walletAccount = wallet.Wallets.FirstOrDefault(w =>
            w.Data.Account == account && w.Data.Authority == authority && w.Data.ChainId == chainId
        );

        if (walletAccount == null)
            return false;

        // Update fields
        if (label != null)
            walletAccount.Data.Label = label;

        if (mode.HasValue)
            walletAccount.Data.Mode = mode.Value;

        await _storageService.SaveWalletAsync(wallet);
        return true;
    }

    /// <summary>
    /// Get decrypted private key for account (in-memory only)
    /// </summary>
    public async Task<string?> GetPrivateKeyAsync(
        string account,
        string authority,
        string chainId,
        string password
    )
    {
        // If no password provided, allow fallback to unlocked wallet state
        if (string.IsNullOrWhiteSpace(password))
        {
            // If wallet isn't unlocked we cannot proceed without a password
            if (!_storageService.IsUnlocked)
                return null;
        }
        else
        {
            // Validate password
            if (!await _storageService.ValidatePasswordAsync(password))
                return null;
        }

        // Get account
        var walletAccount = await GetAccountAsync(account, authority, chainId);
        if (walletAccount == null)
            return null;

        // Unlock wallet to access keys if needed
        if (!_storageService.IsUnlocked)
        {
            if (!await _storageService.UnlockWalletAsync(password))
                return null;
        }

        // Get private key from storage
        return _storageService.GetUnlockedPrivateKey(walletAccount.Data.PublicKey);
    }

    /// <summary>
    /// Set current active account
    /// </summary>
    public async Task SetCurrentAccountAsync(string account, string authority, string chainId)
    {
        _currentAccount = await GetAccountAsync(account, authority, chainId);
    }

    /// <summary>
    /// Get current active account
    /// </summary>
    public Task<WalletAccount?> GetCurrentAccountAsync()
    {
        return Task.FromResult(_currentAccount);
    }

    /// <summary>
    /// Import account from BIP39 mnemonic phrase
    /// </summary>
    public async Task<WalletAccount> ImportAccountFromMnemonicAsync(
        string account,
        string authority,
        string chainId,
        string mnemonic,
        string password,
        int accountIndex = 0,
        string? label = null
    )
    {
        try
        {
            // Derive private key from mnemonic
            var privateKey = _cryptographyService.DeriveKeyFromMnemonic(mnemonic, accountIndex);

            // Import using the derived key
            return await ImportAccountAsync(
                account,
                authority,
                chainId,
                privateKey,
                password,
                label
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to import from mnemonic: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Generate a new account with random key
    /// </summary>
    public async Task<(
        WalletAccount account,
        string privateKey,
        string mnemonic
    )> GenerateNewAccountAsync(
        string account,
        string authority,
        string chainId,
        string password,
        string? label = null
    )
    {
        try
        {
            // Generate mnemonic and derive key
            var mnemonic = _cryptographyService.GenerateMnemonic();
            var privateKey = _cryptographyService.DeriveKeyFromMnemonic(mnemonic, 0);

            // Import the generated account
            var walletAccount = await ImportAccountAsync(
                account,
                authority,
                chainId,
                privateKey,
                password,
                label
            );

            return (walletAccount, privateKey, mnemonic);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to generate new account: {ex.Message}",
                ex
            );
        }
    }
}
