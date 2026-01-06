using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models;

/// <summary>
/// Blockchain network configuration
/// </summary>
public class NetworkConfig
{
    [JsonPropertyName("chainId")]
    public string ChainId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("httpEndpoint")]
    public string HttpEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("keyPrefix")]
    public string KeyPrefix { get; set; } = "EOS";

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "EOS";

    [JsonPropertyName("precision")]
    public int Precision { get; set; } = 4;

    [JsonPropertyName("blockExplorer")]
    public string? BlockExplorer { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
