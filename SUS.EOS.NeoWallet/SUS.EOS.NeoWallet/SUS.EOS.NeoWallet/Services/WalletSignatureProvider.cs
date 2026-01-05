using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Services;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Providers;
using SUS.EOS.Sharp.Cryptography;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Wallet-integrated signature provider
/// Combines EosioSignatureProvider with WalletAccountService for seamless signing
/// </summary>
public class WalletSignatureProvider : ISignatureProvider
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly string? _password;
    private readonly Dictionary<string, EosioKey> _keyCache = new();

    public WalletSignatureProvider(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        string? password = null)
    {
        _accountService = accountService;
        _storageService = storageService;
        _password = password;
    }

    /// <summary>
    /// Get available public keys from current account
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAvailableKeysAsync(CancellationToken cancellationToken = default)
    {
        var currentAccount = await _accountService.GetCurrentAccountAsync();
        if (currentAccount == null)
            return Array.Empty<string>();

        return new[] { currentAccount.Data.PublicKey };
    }

    /// <summary>
    /// Sign transaction using wallet's private key
    /// </summary>
    public async Task<IReadOnlyList<string>> SignAsync(
        string chainId,
        IEnumerable<string> requiredKeys,
        byte[] signBytes,
        CancellationToken cancellationToken = default)
    {
        var currentAccount = await _accountService.GetCurrentAccountAsync();
        if (currentAccount == null)
            throw new InvalidOperationException("No active account");

        // Get private key - always use password-based decryption
        if (string.IsNullOrEmpty(_password))
            throw new UnauthorizedAccessException("Wallet is locked. Password required.");

        var privateKey = await _accountService.GetPrivateKeyAsync(
            currentAccount.Data.Account,
            currentAccount.Data.Authority,
            currentAccount.Data.ChainId,
            _password)
            ?? throw new InvalidOperationException("Failed to decrypt private key");

        // Get or create key for signing
        if (!_keyCache.TryGetValue(privateKey, out var key))
        {
            key = EosioKey.FromWif(privateKey);
            _keyCache[privateKey] = key;
        }

        // Create signing data with chain ID
        var chainIdBytes = EosioSerializer.HexStringToBytes(chainId);
        var signingData = new byte[chainIdBytes.Length + signBytes.Length + 32];
        Buffer.BlockCopy(chainIdBytes, 0, signingData, 0, chainIdBytes.Length);
        Buffer.BlockCopy(signBytes, 0, signingData, chainIdBytes.Length, signBytes.Length);
        
        // Hash and sign
        var hash = EosioSerializer.Sha256(signingData);
        var compactSignature = key.SignCompact(hash);
        
        // Encode signature in EOSIO format
        var signature = EncodeSignature(compactSignature);
        
        return new[] { signature };
    }

    private static string EncodeSignature(byte[] compactSignature)
    {
        var keyTypeBytes = System.Text.Encoding.ASCII.GetBytes("K1");
        var checkData = new byte[compactSignature.Length + keyTypeBytes.Length];
        Buffer.BlockCopy(compactSignature, 0, checkData, 0, compactSignature.Length);
        Buffer.BlockCopy(keyTypeBytes, 0, checkData, compactSignature.Length, keyTypeBytes.Length);
        
        var checksum = Ripemd160(checkData);
        
        var signatureBytes = new byte[compactSignature.Length + 4];
        Buffer.BlockCopy(compactSignature, 0, signatureBytes, 0, compactSignature.Length);
        Buffer.BlockCopy(checksum, 0, signatureBytes, compactSignature.Length, 4);
        
        return "SIG_K1_" + Base58Encode(signatureBytes);
    }

    private static byte[] Ripemd160(byte[] data)
    {
        var digest = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var result = new byte[20];
        digest.DoFinal(result, 0);
        return result;
    }

    private static string Base58Encode(byte[] data)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var result = new System.Text.StringBuilder();
        var value = new Org.BouncyCastle.Math.BigInteger(1, data);
        var baseValue = Org.BouncyCastle.Math.BigInteger.ValueOf(58);
        
        while (value.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) > 0)
        {
            var divRem = value.DivideAndRemainder(baseValue);
            value = divRem[0];
            result.Insert(0, alphabet[divRem[1].IntValue]);
        }
        
        foreach (var b in data)
        {
            if (b == 0) result.Insert(0, '1');
            else break;
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Create provider for currently selected account
    /// </summary>
    public static WalletSignatureProvider CreateForCurrentAccount(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        string? password = null)
    {
        return new WalletSignatureProvider(accountService, storageService, password);
    }
}
