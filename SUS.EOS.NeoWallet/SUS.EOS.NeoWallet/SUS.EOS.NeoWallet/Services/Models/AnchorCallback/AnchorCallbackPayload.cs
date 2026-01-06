namespace SUS.EOS.NeoWallet.Services.Models.AnchorCallback;

/// <summary>
/// Anchor callback payload (sent to external application)
/// </summary>
public class AnchorCallbackPayload
{
    public string? Account { get; set; }
    public string? Permission { get; set; }
    public string? PublicKey { get; set; }
    public string? ChainId { get; set; }
    public List<string>? Signatures { get; set; }
    public string? TransactionId { get; set; }
    public object? Transaction { get; set; }
    public uint? BlockNum { get; set; }
    public string? BlockId { get; set; }
}
