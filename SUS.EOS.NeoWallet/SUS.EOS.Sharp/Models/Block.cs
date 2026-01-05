using System.Text.Json.Serialization;

namespace SUS.EOS.Sharp.Models;

/// <summary>
/// EOSIO block information
/// </summary>
public record Block
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("producer")]
    public string Producer { get; init; } = string.Empty;

    [JsonPropertyName("confirmed")]
    public int Confirmed { get; init; }

    [JsonPropertyName("previous")]
    public string Previous { get; init; } = string.Empty;

    [JsonPropertyName("transaction_mroot")]
    public string TransactionMroot { get; init; } = string.Empty;

    [JsonPropertyName("action_mroot")]
    public string ActionMroot { get; init; } = string.Empty;

    [JsonPropertyName("schedule_version")]
    public int ScheduleVersion { get; init; }

    [JsonPropertyName("new_producers")]
    public object? NewProducers { get; init; }

    [JsonPropertyName("producer_signature")]
    public string ProducerSignature { get; init; } = string.Empty;

    [JsonPropertyName("transactions")]
    public List<BlockTransaction> Transactions { get; init; } = new();

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("block_num")]
    public uint BlockNum { get; init; }

    [JsonPropertyName("ref_block_prefix")]
    public uint RefBlockPrefix { get; init; }
}

/// <summary>
/// Transaction in a block
/// </summary>
public record BlockTransaction
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("cpu_usage_us")]
    public int CpuUsageUs { get; init; }

    [JsonPropertyName("net_usage_words")]
    public int NetUsageWords { get; init; }

    [JsonPropertyName("trx")]
    public object? Trx { get; init; }
}