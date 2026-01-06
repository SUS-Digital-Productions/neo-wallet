using System.Text.Json.Serialization;

namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR Callback Payload (sent after signing)
/// </summary>
public class EsrCallbackPayload
{
    [JsonPropertyName("sig")]
    public string? Signature { get; set; }

    [JsonPropertyName("tx")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("sa")]
    public string? SignerActor { get; set; }

    [JsonPropertyName("sp")]
    public string? SignerPermission { get; set; }

    [JsonPropertyName("bn")]
    public uint? BlockNum { get; set; }

    [JsonPropertyName("ex")]
    public string? Expiration { get; set; }

    [JsonPropertyName("link_ch")]
    public string? LinkChannel { get; set; }

    [JsonPropertyName("link_key")]
    public string? LinkKey { get; set; }

    [JsonPropertyName("link_name")]
    public string? LinkName { get; set; }
}
