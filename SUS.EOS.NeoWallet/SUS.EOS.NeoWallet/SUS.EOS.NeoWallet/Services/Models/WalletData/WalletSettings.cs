using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

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
