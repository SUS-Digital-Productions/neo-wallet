using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models.WalletData;

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
