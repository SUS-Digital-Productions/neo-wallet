using System.Text.Json.Serialization;

namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR Session - represents a linked dApp
/// </summary>
public class EsrSession
{
    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = "";

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = "";

    [JsonPropertyName("permission")]
    public string Permission { get; set; } = "";

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }
}
