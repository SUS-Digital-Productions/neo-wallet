using SUS.EOS.EosioSigningRequest;
using SUS.EOS.EosioSigningRequest.Models;

namespace SUS.EOS.NeoWallet.Services.Models.AnchorCallback;

/// <summary>
/// Result of Anchor callback processing
/// </summary>
public class AnchorCallbackResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public EsrCallbackResponse? Response { get; set; }
    public string? Account { get; set; }
    public string? Permission { get; set; }
    public string? ChainId { get; set; }
}
