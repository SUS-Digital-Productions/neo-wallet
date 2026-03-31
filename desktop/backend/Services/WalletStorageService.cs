using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Encrypted wallet file model persisted to disk.
/// </summary>
public sealed class WalletFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("iv")]
    public string Iv { get; set; } = "";

    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; set; } = "";
}

/// <summary>
/// Plaintext wallet data decrypted in memory while unlocked.
/// </summary>
public sealed class WalletData
{
    [JsonPropertyName("accounts")]
    public List<WalletAccount> Accounts { get; set; } = [];

    [JsonPropertyName("keys")]
    public List<WalletKey> Keys { get; set; } = [];
}

/// <summary>
/// A standalone private key stored in the wallet (not necessarily linked to an imported account).
/// </summary>
public sealed class WalletKey
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("privateKeyWif")]
    public string PrivateKeyWif { get; set; } = "";

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = "";
}

/// <summary>
/// A single account entry stored in the wallet.
/// </summary>
public sealed class WalletAccount
{
    [JsonPropertyName("account")]
    public string Account { get; set; } = "";

    [JsonPropertyName("authority")]
    public string Authority { get; set; } = "active";

    [JsonPropertyName("privateKeyWif")]
    public string PrivateKeyWif { get; set; } = "";

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = "";

    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = "";
}

/// <summary>
/// Manages encrypted wallet file on disk-scoped storage.
/// Uses AES-256-CBC with PBKDF2 key derivation.
/// </summary>
public interface IWalletStorageService
{
    bool WalletFileExists { get; }
    WalletData? CreateWallet(string password);
    WalletData? Unlock(string password);
    void Lock();
    void Save(string password, WalletData data);
    WalletData? CurrentData { get; }
    byte[]? ReadRawFile();
    void WriteRawFile(byte[] data);
}

public sealed class WalletStorageService : IWalletStorageService
{
    private const int SaltSize = 16;
    private const int KeySize = 32; // AES-256
    private const int IvSize = 16;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlg = HashAlgorithmName.SHA256;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _walletPath;
    private WalletData? _current;

    public WalletStorageService(IConfiguration configuration)
    {
        var dir = configuration["Wallet:Directory"]
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoWallet");
        Directory.CreateDirectory(dir);
        _walletPath = Path.Combine(dir, "wallet.json");
        System.Diagnostics.Trace.WriteLine($"[WALLETSTORAGE] Wallet path: {_walletPath}");
    }

    public bool WalletFileExists => File.Exists(_walletPath);
    public WalletData? CurrentData => _current;

    public WalletData? CreateWallet(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        _current = new WalletData();
        Save(password, _current);
        System.Diagnostics.Trace.WriteLine("[WALLETSTORAGE] New wallet created");
        return _current;
    }

    public WalletData? Unlock(string password)
    {
        if (!File.Exists(_walletPath))
            return null;

        try
        {
            var json = File.ReadAllText(_walletPath);
            var file = JsonSerializer.Deserialize<WalletFile>(json);
            if (file is null) return null;

            var salt = Convert.FromBase64String(file.Salt);
            var iv = Convert.FromBase64String(file.Iv);
            var ciphertext = Convert.FromBase64String(file.Ciphertext);

            var key = DeriveKey(password, salt);
            var plaintext = Decrypt(ciphertext, key, iv);

            _current = JsonSerializer.Deserialize<WalletData>(plaintext);
            System.Diagnostics.Trace.WriteLine($"[WALLETSTORAGE] Wallet unlocked, {_current?.Accounts.Count ?? 0} accounts");
            return _current;
        }
        catch (CryptographicException)
        {
            System.Diagnostics.Trace.WriteLine("[WALLETSTORAGE] Unlock failed: wrong password");
            return null;
        }
    }

    public void Lock()
    {
        _current = null;
        System.Diagnostics.Trace.WriteLine("[WALLETSTORAGE] Wallet locked");
    }

    public void Save(string password, WalletData data)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var key = DeriveKey(password, salt);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(data, JsonOpts);
        var ciphertext = Encrypt(plaintext, key, iv);

        var file = new WalletFile
        {
            Salt = Convert.ToBase64String(salt),
            Iv = Convert.ToBase64String(iv),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };

        var json = JsonSerializer.Serialize(file, JsonOpts);
        File.WriteAllText(_walletPath, json);
        _current = data;
        System.Diagnostics.Trace.WriteLine("[WALLETSTORAGE] Wallet saved to disk");
    }

    public byte[]? ReadRawFile()
    {
        return File.Exists(_walletPath) ? File.ReadAllBytes(_walletPath) : null;
    }

    public void WriteRawFile(byte[] data)
    {
        // Lock the current wallet before overwriting
        _current = null;
        File.WriteAllBytes(_walletPath, data);
        System.Diagnostics.Trace.WriteLine("[WALLETSTORAGE] Wallet file replaced via import");
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlg, KeySize);
    }

    private static byte[] Encrypt(byte[] plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var enc = aes.CreateEncryptor();
        return enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }

    private static byte[] Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }
}
