using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

/// <summary>
/// Wallet data structure inspired by Anchor wallet's schema system
/// Supports secure storage with encrypted private keys and multi-chain wallets
/// </summary>
public class WalletData
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "neowallet.v1.storage";

    [JsonPropertyName("metadata")]
    public WalletMetadata Metadata { get; set; } = new();

    [JsonPropertyName("networks")]
    public Dictionary<string, NetworkConfig> Networks { get; set; } = new();

    [JsonPropertyName("storage")]
    public EncryptedStorage Storage { get; set; } = new();

    [JsonPropertyName("wallets")]
    public List<WalletAccount> Wallets { get; set; } = new();

    [JsonPropertyName("settings")]
    public WalletSettings Settings { get; set; } = new();
}

/// <summary>
/// Wallet file metadata and version information
/// </summary>
public class WalletMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updated")]
    public DateTime Updated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("application")]
    public string Application { get; set; } = "SUS.EOS.NeoWallet";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Blockchain network configuration
/// </summary>
public class NetworkConfig
{
    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("httpEndpoint")]
    public string HttpEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("keyPrefix")]
    public string KeyPrefix { get; set; } = "EOS";

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "EOS";

    [JsonPropertyName("precision")]
    public int Precision { get; set; } = 4;

    [JsonPropertyName("blockExplorer")]
    public string? BlockExplorer { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Encrypted storage container for private keys
/// Uses AES-256 encryption with PBKDF2 key derivation (similar to Anchor)
/// </summary>
public class EncryptedStorage
{
    [JsonPropertyName("data")]
    public string? EncryptedData { get; set; }

    [JsonPropertyName("keys")]
    public List<string> PublicKeys { get; set; } = new();

    [JsonPropertyName("paths")]
    public Dictionary<string, string> HardwarePaths { get; set; } = new();

    [JsonPropertyName("encryption")]
    public EncryptionInfo Encryption { get; set; } = new();
}

/// <summary>
/// Encryption parameters and algorithm information
/// </summary>
public class EncryptionInfo
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "AES-256-CBC";

    [JsonPropertyName("keyDerivation")]
    public string KeyDerivation { get; set; } = "PBKDF2";

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; } = 4500;

    [JsonPropertyName("keySize")]
    public int KeySize { get; set; } = 256;

    [JsonPropertyName("ivSize")]
    public int IvSize { get; set; } = 128;
}

/// <summary>
/// Individual wallet account configuration
/// Based on Anchor's anchor.v2.wallet schema
/// </summary>
public class WalletAccount
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "neowallet.v1.wallet";

    [JsonPropertyName("data")]
    public WalletAccountData Data { get; set; } = new();
}

/// <summary>
/// Wallet account data structure
/// </summary>
public class WalletAccountData
{
    [JsonPropertyName("account")]
    public string Account { get; set; } = string.Empty;

    [JsonPropertyName("authority")]
    public string Authority { get; set; } = "active";

    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = string.Empty;

    [JsonPropertyName("pubkey")]
    public string PublicKey { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public WalletMode Mode { get; set; } = WalletMode.Hot;

    [JsonPropertyName("type")]
    public WalletType Type { get; set; } = WalletType.Key;

    [JsonPropertyName("path")]
    public string? HardwarePath { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Wallet operation mode (following Anchor patterns)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletMode
{
    Hot,      // Private key stored encrypted
    Cold,     // Offline signing mode
    Watch,    // View-only mode
    Ledger    // Hardware wallet mode
}

/// <summary>
/// Wallet type classification
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletType
{
    Key,      // Standard private key
    Ledger,   // Hardware wallet
    Imported, // Imported from external source
    Generated // Generated by this wallet
}

/// <summary>
/// Wallet application settings
/// </summary>
public class WalletSettings
{
    [JsonPropertyName("defaultNetwork")]
    public string? DefaultNetwork { get; set; }

    [JsonPropertyName("autoLock")]
    public bool AutoLock { get; set; } = true;

    [JsonPropertyName("lockTimeout")]
    public int LockTimeoutMinutes { get; set; } = 15;

    [JsonPropertyName("showTestnets")]
    public bool ShowTestnets { get; set; } = false;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "auto";
}

/// <summary>
/// Decrypted keypair for in-memory use only (never serialized to JSON)
/// </summary>
public class KeyPair
{
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string? Label { get; set; }
}