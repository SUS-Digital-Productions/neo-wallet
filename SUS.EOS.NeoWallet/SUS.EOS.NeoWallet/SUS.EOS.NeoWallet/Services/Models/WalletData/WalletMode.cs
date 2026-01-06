using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models.WalletData;

/// <summary>
/// Wallet operation mode (following Anchor patterns)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletMode
{
    Hot, // Private key stored encrypted
    Cold, // Offline signing mode
    Watch, // View-only mode
    Ledger, // Hardware wallet mode
}
