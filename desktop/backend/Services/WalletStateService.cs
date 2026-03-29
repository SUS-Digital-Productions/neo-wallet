using NeoWallet.Backend.Dto;
using SUS.EOS.Sharp.Cryptography;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Wallet state service backed by encrypted file storage.
/// </summary>
public sealed class WalletStateService : IWalletStateService
{
    private static readonly List<NetworkDto> Networks =
    [
        new("1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", "WAX Mainnet", "WAX"),
        new("aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", "EOS Mainnet", "EOS"),
        new("4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", "Telos Mainnet", "TLOS"),
    ];

    private readonly IWalletStorageService _storage;
    private NetworkDto _activeNetwork = Networks[0];
    private AccountDto? _activeAccount;
    private string? _password;

    public WalletStateService(IWalletStorageService storage)
    {
        _storage = storage;
    }

    public bool WalletLoaded => _storage.WalletFileExists;
    public bool WalletUnlocked => _storage.CurrentData is not null;

    public NetworkDto? ActiveNetwork => _activeNetwork;
    public AccountDto? ActiveAccount => _activeAccount;

    public IReadOnlyList<AccountDto> GetAccounts()
    {
        var data = _storage.CurrentData;
        if (data is null) return [];
        return data.Accounts
            .Select(a => new AccountDto(a.Account, a.Authority, a.PublicKey))
            .ToList();
    }

    public IReadOnlyList<NetworkDto> GetNetworks() => Networks;

    public void SetActiveAccount(string account, string authority, string chainId)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var match = data.Accounts.Find(a => a.Account == account && a.Authority == authority);
        if (match is null)
            throw new InvalidOperationException($"Account {account}@{authority} not found.");
        _activeAccount = new AccountDto(match.Account, match.Authority, match.PublicKey);
        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Active account set to {account}@{authority}");
    }

    public void SetActiveNetwork(string chainId)
    {
        var match = Networks.Find(n => n.ChainId == chainId);
        if (match is null)
            throw new InvalidOperationException($"Network {chainId} not found.");
        _activeNetwork = match;
        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Active network set to {match.Name}");
    }

    public bool CreateWallet(string password)
    {
        var data = _storage.CreateWallet(password);
        if (data is null) return false;
        _password = password;
        System.Diagnostics.Trace.WriteLine("[WALLETSTATE] New wallet created");
        return true;
    }

    public bool Unlock(string password)
    {
        var data = _storage.Unlock(password);
        if (data is null) return false;
        _password = password;

        // Restore first account as active if available
        if (data.Accounts.Count > 0)
        {
            var first = data.Accounts[0];
            _activeAccount = new AccountDto(first.Account, first.Authority, first.PublicKey);
        }

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Wallet unlocked, {data.Accounts.Count} accounts");
        return true;
    }

    public void Lock()
    {
        _storage.Lock();
        _password = null;
        _activeAccount = null;
        System.Diagnostics.Trace.WriteLine("[WALLETSTATE] Wallet locked");
    }

    public AccountDto ImportAccount(string privateKeyWif, string account, string authority, string password)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");

        // Validate the private key and derive the public key
        var key = EosioKey.FromPrivateKey(privateKeyWif);
        var publicKey = key.PublicKey;

        // Check for duplicates
        if (data.Accounts.Any(a => a.Account == account && a.Authority == authority))
            throw new InvalidOperationException($"Account {account}@{authority} already exists.");

        data.Accounts.Add(new WalletAccount
        {
            Account = account,
            Authority = authority,
            PrivateKeyWif = key.PrivateKeyWif,
            PublicKey = publicKey,
        });

        _storage.Save(password, data);

        var dto = new AccountDto(account, authority, publicKey);
        _activeAccount ??= dto;

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Imported {account}@{authority} ({publicKey[..16]}…)");
        return dto;
    }

    public bool RemoveAccount(string account, string authority, string password)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var removed = data.Accounts.RemoveAll(a => a.Account == account && a.Authority == authority);
        if (removed == 0) return false;

        _storage.Save(password, data);

        // Clear active if it was removed
        if (_activeAccount is not null && _activeAccount.Account == account && _activeAccount.Authority == authority)
            _activeAccount = data.Accounts.Count > 0
                ? new AccountDto(data.Accounts[0].Account, data.Accounts[0].Authority, data.Accounts[0].PublicKey)
                : null;

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Removed {account}@{authority}");
        return true;
    }

    public string? GetPrivateKeyWif(string account, string authority)
    {
        var data = _storage.CurrentData;
        if (data is null) return null;
        var match = data.Accounts.Find(a => a.Account == account && a.Authority == authority);
        return match?.PrivateKeyWif;
    }
}
