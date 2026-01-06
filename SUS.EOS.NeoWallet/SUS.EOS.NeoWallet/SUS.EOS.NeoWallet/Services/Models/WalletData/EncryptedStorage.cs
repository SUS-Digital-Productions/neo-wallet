using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

/// <summary>
/// Encrypted storage container for private keys
/// Uses AES-256 encryption with PBKDF2 key derivation (similar to Anchor)
/// </summary>
public class EncryptedStorage
{
    [JsonPropertyName("data")]
    public string? EncryptedData { get; set; }

    [JsonPropertyName("keys")]
    public List<string> PublicKeys { get; set; } = [];

    [JsonPropertyName("paths")]
    public Dictionary<string, string> HardwarePaths { get; set; } = [];

    [JsonPropertyName("encryption")]
    public EncryptionInfo Encryption { get; set; } = new();
}
