using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

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
