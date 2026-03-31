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

    public string GetChainName(string chainId) =>
        Networks.Find(n => n.ChainId == chainId)?.Name ?? "Unknown";

    public IReadOnlyList<AccountDto> GetAccounts()
    {
        var data = _storage.CurrentData;
        if (data is null) return [];
        return data.Accounts
            .Select(a => new AccountDto(
                a.Account, a.Authority, a.PublicKey,
                a.ChainId, GetChainName(a.ChainId)))
            .ToList();
    }

    public IReadOnlyList<NetworkDto> GetNetworks() => Networks;

    public void SetActiveAccount(string account, string authority, string chainId)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var match = data.Accounts.Find(a =>
            a.Account == account && a.Authority == authority && a.ChainId == chainId);
        if (match is null)
            throw new InvalidOperationException($"Account {account}@{authority} on {chainId[..16]}… not found.");
        _activeAccount = new AccountDto(match.Account, match.Authority, match.PublicKey,
            match.ChainId, GetChainName(match.ChainId));
        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Active account set to {account}@{authority} on {GetChainName(chainId)}");
    }

    public void SetActiveNetwork(string chainId)
    {
        var match = Networks.Find(n => n.ChainId == chainId);
        if (match is null)
            throw new InvalidOperationException($"Network {chainId} not found.");
        _activeNetwork = match;

        // Auto-switch to an account on the new chain if the current one doesn't match
        if (_activeAccount is null || _activeAccount.ChainId != chainId)
        {
            var data = _storage.CurrentData;
            var fallback = data?.Accounts.Find(a => a.ChainId == chainId);
            _activeAccount = fallback is not null
                ? new AccountDto(fallback.Account, fallback.Authority, fallback.PublicKey,
                    fallback.ChainId, GetChainName(fallback.ChainId))
                : null;
        }

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Active network set to {match.Name}, account: {_activeAccount?.Account ?? "(none)"}");
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
            _activeAccount = new AccountDto(first.Account, first.Authority, first.PublicKey,
                first.ChainId, GetChainName(first.ChainId));
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

    public IReadOnlyList<AccountDto> ImportAccounts(string privateKeyWif, IEnumerable<ImportAccountEntry> accounts)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var password = _password ?? throw new InvalidOperationException("Wallet is locked.");

        var key = EosioKey.FromPrivateKey(privateKeyWif);
        var publicKey = key.PublicKey;
        var imported = new List<AccountDto>();

        // Auto-add key to the key store if not already present
        if (!data.Keys.Any(k => k.PublicKey == publicKey))
        {
            data.Keys.Add(new WalletKey
            {
                Label = "",
                PrivateKeyWif = key.PrivateKeyWif,
                PublicKey = publicKey,
            });
        }

        foreach (var entry in accounts)
        {
            // Skip duplicates
            if (data.Accounts.Any(a =>
                a.Account == entry.Account && a.Authority == entry.Authority && a.ChainId == entry.ChainId))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[WALLETSTATE] Skipped duplicate {entry.Account}@{entry.Authority} on {GetChainName(entry.ChainId)}");
                continue;
            }

            data.Accounts.Add(new WalletAccount
            {
                Account = entry.Account,
                Authority = entry.Authority,
                PrivateKeyWif = key.PrivateKeyWif,
                PublicKey = publicKey,
                ChainId = entry.ChainId,
            });

            var dto = new AccountDto(entry.Account, entry.Authority, publicKey,
                entry.ChainId, GetChainName(entry.ChainId));
            imported.Add(dto);
            System.Diagnostics.Trace.WriteLine(
                $"[WALLETSTATE] Imported {entry.Account}@{entry.Authority} on {GetChainName(entry.ChainId)}");
        }

        if (imported.Count > 0)
        {
            _storage.Save(password, data);
            _activeAccount ??= imported[0];
        }

        return imported;
    }

    public bool RemoveAccount(string account, string authority, string chainId)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var password = _password ?? throw new InvalidOperationException("Wallet is locked.");

        var removed = data.Accounts.RemoveAll(a =>
            a.Account == account && a.Authority == authority && a.ChainId == chainId);
        if (removed == 0) return false;

        _storage.Save(password, data);

        // Clear active if it was removed
        if (_activeAccount is not null &&
            _activeAccount.Account == account &&
            _activeAccount.Authority == authority &&
            _activeAccount.ChainId == chainId)
        {
            _activeAccount = data.Accounts.Count > 0
                ? new AccountDto(data.Accounts[0].Account, data.Accounts[0].Authority,
                    data.Accounts[0].PublicKey, data.Accounts[0].ChainId,
                    GetChainName(data.Accounts[0].ChainId))
                : null;
        }

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Removed {account}@{authority} on {GetChainName(chainId)}");
        return true;
    }

    public string? GetPrivateKeyWif(string account, string authority)
    {
        var data = _storage.CurrentData;
        if (data is null) return null;
        var match = data.Accounts.Find(a => a.Account == account && a.Authority == authority);
        return match?.PrivateKeyWif;
    }

    public IReadOnlyList<KeyDto> GetKeys()
    {
        var data = _storage.CurrentData;
        if (data is null) return [];
        return data.Keys
            .Select(k => new KeyDto(
                k.PublicKey,
                k.Label,
                data.Accounts.Count(a => a.PublicKey == k.PublicKey)))
            .ToList();
    }

    public KeyDto AddKey(string privateKeyWif, string label)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var password = _password ?? throw new InvalidOperationException("Wallet is locked.");

        var key = EosioKey.FromPrivateKey(privateKeyWif);

        // Prevent duplicate keys
        if (data.Keys.Any(k => k.PublicKey == key.PublicKey))
            throw new InvalidOperationException("This key is already stored.");

        data.Keys.Add(new WalletKey
        {
            Label = label,
            PrivateKeyWif = key.PrivateKeyWif,
            PublicKey = key.PublicKey,
        });

        _storage.Save(password, data);
        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Added key {key.PublicKey[..24]}… label=\"{label}\"");
        return new KeyDto(key.PublicKey, label, data.Accounts.Count(a => a.PublicKey == key.PublicKey));
    }

    public bool RemoveKey(string publicKey)
    {
        var data = _storage.CurrentData ?? throw new InvalidOperationException("Wallet is locked.");
        var password = _password ?? throw new InvalidOperationException("Wallet is locked.");

        var removed = data.Keys.RemoveAll(k => k.PublicKey == publicKey);
        if (removed == 0) return false;

        // Also remove all accounts linked to this key
        var accountsRemoved = data.Accounts.RemoveAll(a => a.PublicKey == publicKey);
        _storage.Save(password, data);

        // Clear active account if it was linked to the removed key
        if (_activeAccount is not null &&
            data.Accounts.All(a => a.Account != _activeAccount.Account || a.Authority != _activeAccount.Authority || a.ChainId != _activeAccount.ChainId))
        {
            _activeAccount = data.Accounts.Count > 0
                ? new AccountDto(data.Accounts[0].Account, data.Accounts[0].Authority,
                    data.Accounts[0].PublicKey, data.Accounts[0].ChainId,
                    GetChainName(data.Accounts[0].ChainId))
                : null;
        }

        System.Diagnostics.Trace.WriteLine($"[WALLETSTATE] Removed key {publicKey[..24]}… and {accountsRemoved} linked accounts");
        return true;
    }
}
