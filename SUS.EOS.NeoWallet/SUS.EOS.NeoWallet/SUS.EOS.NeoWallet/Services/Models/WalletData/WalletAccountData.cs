using System.Text.Json.Serialization;
using SUS.EOS.NeoWallet.Services.Models.WalletData;

namespace SUS.EOS.NeoWallet.Services.Models;

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
