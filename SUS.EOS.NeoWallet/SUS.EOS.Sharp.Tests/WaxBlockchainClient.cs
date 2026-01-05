using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SUS.EOS.Sharp;
using SUS.EOS.Sharp.Models;

namespace SUS.EOS.Sharp.Tests;

/// <summary>
/// WAX blockchain client implementation for testing
/// </summary>
public sealed class WaxBlockchainClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private bool _disposed;

    public WaxBlockchainClient(string endpoint = "https://wax.greymass.com")
    {
        _endpoint = endpoint;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Gets blockchain information
    /// </summary>
    public async Task<WaxChainInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("/v1/chain/get_info", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<WaxChainInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return info ?? throw new InvalidOperationException("Failed to deserialize chain info");
    }

    /// <summary>
    /// Gets account information
    /// </summary>
    public async Task<WaxAccount> GetAccountAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var request = new { account_name = accountName };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/get_account", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var account = JsonSerializer.Deserialize<WaxAccount>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return account ?? throw new InvalidOperationException("Failed to deserialize account");
    }

    /// <summary>
    /// Gets account balance
    /// </summary>
    public async Task<List<string>> GetCurrencyBalanceAsync(
        string contract, 
        string account, 
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            code = contract,
            account = account,
            symbol = symbol
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/get_currency_balance", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var balances = JsonSerializer.Deserialize<List<string>>(json);
        
        return balances ?? new List<string>();
    }

    /// <summary>
    /// Gets table rows from a smart contract
    /// </summary>
    public async Task<WaxTableRows<T>> GetTableRowsAsync<T>(
        string contract,
        string scope,
        string table,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            json = true,
            code = contract,
            scope = scope,
            table = table,
            limit = 100
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/get_table_rows", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<WaxTableRows<T>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return result ?? throw new InvalidOperationException("Failed to deserialize table rows");
    }

    /// <summary>
    /// Converts JSON action arguments to binary using the blockchain's ABI
    /// </summary>
    public async Task<string> AbiJsonToBinAsync(
        string code,
        string action,
        object args,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            code = code,
            action = action,
            args = args
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/abi_json_to_bin", content, cancellationToken);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"AbiJsonToBin Error: {json}");
            response.EnsureSuccessStatusCode();
        }
        
        var result = JsonSerializer.Deserialize<AbiJsonToBinResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return result?.Binargs ?? throw new InvalidOperationException("Failed to convert JSON to binary");
    }

    /// <summary>
    /// Pushes a transaction to the blockchain
    /// </summary>
    public async Task<WaxTransactionResult> PushTransactionAsync(
        WaxPushTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"\n📨 Push Transaction Request:");
        Console.WriteLine(jsonPayload);
        
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/push_transaction", content, cancellationToken);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error Response: {json}");
            response.EnsureSuccessStatusCode();
        }
        
        var result = JsonSerializer.Deserialize<WaxTransactionResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return result ?? throw new InvalidOperationException("Failed to deserialize transaction result");
    }

    /// <summary>
    /// Gets block information including ref_block_prefix using get_block_info (lighter endpoint)
    /// </summary>
    public async Task<WaxBlockInfo> GetBlockInfoAsync(uint blockNum, CancellationToken cancellationToken = default)
    {
        var request = new { block_num = blockNum };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/get_block_info", content, cancellationToken);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"GetBlockInfo Error: {json}");
            response.EnsureSuccessStatusCode();
        }
        
        var block = JsonSerializer.Deserialize<WaxBlockInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return block ?? throw new InvalidOperationException("Failed to deserialize block info");
    }
    
    /// <summary>
    /// Gets the ABI for a smart contract
    /// </summary>
    public async Task<AbiDefinition?> GetAbiAsync(string contractAccount, CancellationToken cancellationToken = default)
    {
        var request = new { account_name = contractAccount };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/v1/chain/get_abi", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GetAbiResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        
        return result?.Abi;
    }

    /// <summary>
    /// Calculates ref_block_prefix from a block ID (bytes 8-11 as little-endian uint32)
    /// </summary>
    public static uint CalculateRefBlockPrefix(string blockId)
    {
        if (string.IsNullOrEmpty(blockId) || blockId.Length < 24)
            throw new ArgumentException("Block ID must be at least 24 hex characters", nameof(blockId));
        
        // Block ID is a hex string. Bytes 8-11 (hex chars 16-24) form ref_block_prefix
        // The bytes in the block ID are in big-endian order, but we need to read them
        // as little-endian for the ref_block_prefix
        var prefixHex = blockId.Substring(16, 8);
        var bytes = Convert.FromHexString(prefixHex);
        
        // Read the 4 bytes as little-endian (no reversal needed - BitConverter.ToUInt32 
        // reads as little-endian on little-endian systems which is standard)
        return BitConverter.ToUInt32(bytes, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

// Response models
public record WaxChainInfo
{
    [JsonPropertyName("server_version")]
    public string ServerVersion { get; init; } = string.Empty;
    
    [JsonPropertyName("chain_id")]
    public string ChainId { get; init; } = string.Empty;
    
    [JsonPropertyName("head_block_num")]
    public long HeadBlockNum { get; init; }
    
    [JsonPropertyName("last_irreversible_block_num")]
    public long LastIrreversibleBlockNum { get; init; }
    
    [JsonPropertyName("last_irreversible_block_id")]
    public string LastIrreversibleBlockId { get; init; } = string.Empty;
    
    [JsonPropertyName("head_block_id")]
    public string HeadBlockId { get; init; } = string.Empty;
    
    [JsonPropertyName("head_block_time")]
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime HeadBlockTime { get; init; }
    
    [JsonPropertyName("head_block_producer")]
    public string HeadBlockProducer { get; init; } = string.Empty;
}

/// <summary>
/// Custom JSON converter that ensures DateTime is treated as UTC
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
            return DateTime.MinValue;
        
        // EOSIO/WAX blockchain always uses UTC times
        // Parse as UTC directly using DateTimeStyles.AssumeUniversal | AdjustToUniversal
        // This ensures the string is interpreted as UTC, not local time
        if (DateTime.TryParse(dateString, 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dt))
        {
            return dt;
        }
        
        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
    }
}

public record WaxAccount
{
    [JsonPropertyName("account_name")]
    public string AccountName { get; init; } = string.Empty;
    
    [JsonPropertyName("created")]
    public DateTime Created { get; init; }
    
    [JsonPropertyName("ram_quota")]
    public long RamQuota { get; init; }
    
    [JsonPropertyName("ram_usage")]
    public long RamUsage { get; init; }
    
    [JsonPropertyName("cpu_limit")]
    public WaxResourceLimit CpuLimit { get; init; } = new();
    
    [JsonPropertyName("net_limit")]
    public WaxResourceLimit NetLimit { get; init; } = new();
}

public record WaxResourceLimit
{
    [JsonPropertyName("used")]
    public long Used { get; init; }
    
    [JsonPropertyName("available")]
    public long Available { get; init; }
    
    [JsonPropertyName("max")]
    public long Max { get; init; }
}

public record WaxTableRows<T>
{
    public List<T> Rows { get; init; } = new();
    public bool More { get; init; }
}

public record WaxPushTransactionRequest
{
    [JsonPropertyName("signatures")]
    public List<string> Signatures { get; init; } = new();
    
    [JsonPropertyName("compression")]
    public int Compression { get; init; } = 0;
    
    [JsonPropertyName("packed_context_free_data")]
    public string PackedContextFreeData { get; init; } = string.Empty;
    
    [JsonPropertyName("packed_trx")]
    public string PackedTrx { get; init; } = string.Empty;
}

public record WaxTransactionResult
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; init; } = string.Empty;
    
    [JsonPropertyName("processed")]
    public WaxProcessedTransaction Processed { get; init; } = new();
}

public record WaxProcessedTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("block_num")]
    public long BlockNum { get; init; }
    public DateTime BlockTime { get; init; }
}

public record AbiJsonToBinResult
{
    [JsonPropertyName("binargs")]
    public string Binargs { get; init; } = string.Empty;
}

public record WaxBlockInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("block_num")]
    public uint BlockNum { get; init; }
    
    [JsonPropertyName("ref_block_prefix")]
    public uint RefBlockPrefix { get; init; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
    
    [JsonPropertyName("producer")]
    public string Producer { get; init; } = string.Empty;
}
