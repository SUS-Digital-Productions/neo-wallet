using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

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
