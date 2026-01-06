using System.Text.Json.Serialization;

namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR Message Envelope (from WebSocket)
/// Anchor Link sends encrypted messages with type="sealed_message"
/// </summary>
public class EsrMessageEnvelope
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("callback")]
    public string? Callback { get; set; }
    
    /// <summary>
    /// Public key of sender (for sealed_message type)
    /// Used with wallet's private key to derive shared secret for decryption
    /// </summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }
    
    /// <summary>
    /// Nonce for decryption (for sealed_message type)
    /// </summary>
    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }
    
    /// <summary>
    /// Ciphertext (for sealed_message type) - alternative to payload
    /// </summary>
    [JsonPropertyName("ciphertext")]
    public string? Ciphertext { get; set; }
}
