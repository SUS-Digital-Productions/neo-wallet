using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

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
