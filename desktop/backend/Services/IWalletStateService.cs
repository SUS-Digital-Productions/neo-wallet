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
    void SetNetworkNode(string chainId, string nodeUrl);

    /// <summary>Create a new encrypted wallet file.</summary>
    bool CreateWallet(string password);

    /// <summary>Unlock the wallet with the user's password.</summary>
    bool Unlock(string password);

    /// <summary>Lock the wallet (clear decrypted keys from memory).</summary>
    void Lock();

    /// <summary>Import accounts by private key. Wallet must be unlocked. Uses stored password.</summary>
    IReadOnlyList<AccountDto> ImportAccounts(string privateKeyWif, IEnumerable<ImportAccountEntry> accounts);

    /// <summary>Import accounts by an already stored public key without exposing the private key.</summary>
    IReadOnlyList<AccountDto> ImportAccountsByPublicKey(string publicKey, IEnumerable<ImportAccountEntry> accounts);

    /// <summary>Remove an imported account. Uses stored password.</summary>
    bool RemoveAccount(string account, string authority, string chainId);

    /// <summary>
    /// Retrieve the WIF private key for signing. Returns null when unavailable.
    /// </summary>
    string? GetPrivateKeyWif(string account, string authority, string? chainId = null);

    /// <summary>Get all stored standalone keys.</summary>
    IReadOnlyList<KeyDto> GetKeys();

    /// <summary>Add a standalone private key to the wallet.</summary>
    KeyDto AddKey(string privateKeyWif, string label);

    /// <summary>Remove a stored key by public key. Also removes all accounts linked to that key.</summary>
    bool RemoveKey(string publicKey);

    /// <summary>Get display name for a chain ID.</summary>
    string GetChainName(string chainId);
}
