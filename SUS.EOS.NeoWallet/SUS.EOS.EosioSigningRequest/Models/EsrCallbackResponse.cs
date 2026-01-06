namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR callback response
/// </summary>
public class EsrCallbackResponse
{
    public List<string> Signatures { get; set; } = new();
    public object? Transaction { get; set; }
    public byte[]? SerializedTransaction { get; set; }
    public string? PackedTransaction { get; set; }
    public string ChainId { get; set; } = string.Empty;
    
    // Optional metadata
    public uint? BlockNum { get; set; }
    public string? BlockId { get; set; }
    public string? TransactionId { get; set; }
    public string? Signer { get; set; }
    public string? SignerPermission { get; set; }
    public uint? RefBlockNum { get; set; }
    public uint? RefBlockPrefix { get; set; }
    public string? RefBlockId { get; set; }
}
