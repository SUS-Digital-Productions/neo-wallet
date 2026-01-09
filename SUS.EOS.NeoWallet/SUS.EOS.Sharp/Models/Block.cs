using System.Text.Json.Serialization;

namespace SUS.EOS.Sharp.Models;

/// <summary>
/// EOSIO block information
/// </summary>
public record Block
{
    /// <summary>
    /// Block timestamp (UTC)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Producer account that produced the block
    /// </summary>
    [JsonPropertyName("producer")]
    public string Producer { get; init; } = string.Empty;

    /// <summary>
    /// Confirmed field from chain info
    /// </summary>
    [JsonPropertyName("confirmed")]
    public int Confirmed { get; init; }

    /// <summary>
    /// Previous block id
    /// </summary>
    [JsonPropertyName("previous")]
    public string Previous { get; init; } = string.Empty;

    /// <summary>
    /// Transaction merkle root
    /// </summary>
    [JsonPropertyName("transaction_mroot")]
    public string TransactionMroot { get; init; } = string.Empty;

    /// <summary>
    /// Action merkle root
    /// </summary>
    [JsonPropertyName("action_mroot")]
    public string ActionMroot { get; init; } = string.Empty;

    /// <summary>
    /// Producer schedule version
    /// </summary>
    [JsonPropertyName("schedule_version")]
    public int ScheduleVersion { get; init; }

    /// <summary>
    /// Optional new producers list
    /// </summary>
    [JsonPropertyName("new_producers")]
    public object? NewProducers { get; init; }

    /// <summary>
    /// Producer signature for the block
    /// </summary>
    [JsonPropertyName("producer_signature")]
    public string ProducerSignature { get; init; } = string.Empty;

    /// <summary>
    /// Transactions included in the block
    /// </summary>
    [JsonPropertyName("transactions")]
    public List<BlockTransaction> Transactions { get; init; } = new();

    /// <summary>
    /// Block id (hex)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Block number
    /// </summary>
    [JsonPropertyName("block_num")]
    public uint BlockNum { get; init; }

    /// <summary>
    /// Calculated ref block prefix for TAPOS
    /// </summary>
    [JsonPropertyName("ref_block_prefix")]
    public uint RefBlockPrefix { get; init; }
}

/// <summary>
/// Transaction in a block
/// </summary>
public record BlockTransaction
{
    /// <summary>
    /// Transaction status (e.g., 'executed')
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// CPU usage in microseconds
    /// </summary>
    [JsonPropertyName("cpu_usage_us")]
    public int CpuUsageUs { get; init; }

    /// <summary>
    /// Net usage in 64-bit words
    /// </summary>
    [JsonPropertyName("net_usage_words")]
    public int NetUsageWords { get; init; }

    /// <summary>
    /// Transaction data (can be packed or unpacked JSON)
    /// </summary>
    [JsonPropertyName("trx")]
    public object? Trx { get; init; }
}