using NeoWallet.Backend.Dto;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Manages wallet state: lock/unlock, active account, active network.
/// </summary>
public interface IWalletStateService
{
    bool WalletLoaded { get; }
    bool WalletUnlocked { get; }

    NetworkDto? ActiveNetwork { get; }
    AccountDto? ActiveAccount { get; }

    IReadOnlyList<AccountDto> GetAccounts();
    IReadOnlyList<NetworkDto> GetNetworks();

    void SetActiveAccount(string account, string authority, string chainId);
    void SetActiveNetwork(string chainId);

    /// <summary>Create a new encrypted wallet file.</summary>
    bool CreateWallet(string password);

    /// <summary>Unlock the wallet with the user's password.</summary>
    bool Unlock(string password);

    /// <summary>Lock the wallet (clear decrypted keys from memory).</summary>
    void Lock();

    /// <summary>Import an account by private key. Wallet must be unlocked.</summary>
    AccountDto ImportAccount(string privateKeyWif, string account, string authority, string password);

    /// <summary>Remove an imported account.</summary>
    bool RemoveAccount(string account, string authority, string password);

    /// <summary>
    /// Retrieve the WIF private key for signing. Returns null when unavailable.
    /// </summary>
    string? GetPrivateKeyWif(string account, string authority);
}
