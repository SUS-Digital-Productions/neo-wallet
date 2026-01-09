using System.Text.Json;
using System.Text.Json.Serialization;
using SUS.EOS.Sharp.Models;

namespace SUS.EOS.Sharp.Services;

/// <summary>
/// Generic Antelope blockchain client interface
/// Supports any EOSIO/Antelope-based blockchain
/// </summary>
public interface IAntelopeBlockchainClient : IDisposable
{
    /// <summary>
    /// Gets blockchain information
    /// </summary>
    Task<ChainInfo> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets account information by name
    /// </summary>
    Task<Account> GetAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets block information by height or ID
    /// </summary>
    Task<Block> GetBlockAsync(string blockNumOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a signed transaction to the blockchain
    /// </summary>
    Task<TransactionResult> PushTransactionAsync(
        object signedTransaction,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets table rows from a smart contract
    /// </summary>
    Task<TableRowsResult<T>> GetTableRowsAsync<T>(
        string contract,
        string scope,
        string table,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets currency balance for an account
    /// </summary>
    Task<List<string>> GetCurrencyBalanceAsync(
        string contract,
        string account,
        string? symbol = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the ABI for a smart contract
    /// </summary>
    Task<AbiDefinition?> GetAbiAsync(
        string contractAccount,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Converts JSON action data to binary using the chain's serialization
    /// Useful as a fallback when local serialization doesn't work
    /// </summary>
    Task<byte[]> AbiJsonToBinAsync(
        string contract,
        string action,
        object data,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Converts binary action data back to JSON
    /// </summary>
    Task<object?> AbiBinToJsonAsync(
        string contract,
        string action,
        string binArgs,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Network endpoint URL
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// Network chain ID
    /// </summary>
    string? ChainId { get; }
}

/// <summary>
/// HTTP-based Antelope blockchain client implementation
/// Compatible with any EOSIO/Antelope-based network
/// </summary>
public sealed class AntelopeHttpClient : IAntelopeBlockchainClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Network endpoint URL (base address)
    /// </summary>
    public string Endpoint => _endpoint;

    /// <summary>
    /// Chain ID for the connected network (if known)
    /// </summary>
    public string? ChainId { get; private set; }

    /// <summary>
    /// Creates a new HTTP client for the specified endpoint
    /// </summary>
    /// <param name="endpoint">Base URL for the Antelope node (e.g., https://wax.greymass.com)</param>
    public AntelopeHttpClient(string endpoint)
    {
        _endpoint = endpoint;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromSeconds(30),
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
    }

    /// <summary>
    /// Gets blockchain information
    /// </summary>
    public async Task<ChainInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("/v1/chain/get_info", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<ChainInfo>(json, _jsonOptions);

        if (info == null)
            throw new InvalidOperationException("Failed to deserialize chain info");

        // Cache chain ID for convenience
        ChainId = info.ChainId;

        // Calculate RefBlockPrefix from LastIrreversibleBlockId for TAPOS
        var refBlockPrefix = CalculateRefBlockPrefix(info.LastIrreversibleBlockId);
        
        System.Diagnostics.Trace.WriteLine($"[TAPOS] LastIrreversibleBlockId: {info.LastIrreversibleBlockId}");
        System.Diagnostics.Trace.WriteLine($"[TAPOS] LastIrreversibleBlockNum: {info.LastIrreversibleBlockNum}");
        System.Diagnostics.Trace.WriteLine($"[TAPOS] RefBlockPrefix calculated: {refBlockPrefix}");
        System.Diagnostics.Trace.WriteLine($"[TAPOS] RefBlockNum (& 0xFFFF): {info.LastIrreversibleBlockNum & 0xFFFF}");
        
        // Return info with calculated RefBlockPrefix
        return info with { RefBlockPrefix = refBlockPrefix };
    }
    
    /// <summary>
    /// Calculate ref_block_prefix from block ID
    /// </summary>
    private static uint CalculateRefBlockPrefix(string blockId)
    {
        if (string.IsNullOrEmpty(blockId) || blockId.Length < 24)
            return 0;

        // Block ID is 64 hex characters (32 bytes)
        // ref_block_prefix is bytes 8-11 (characters 16-23 in hex)
        // Read directly as little-endian uint32
        var prefixHex = blockId.Substring(16, 8);
        var prefixBytes = Convert.FromHexString(prefixHex);
        
        return BitConverter.ToUInt32(prefixBytes);
    }

    /// <summary>
    /// Gets account information by name
    /// </summary>
    public async Task<Account> GetAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default
    )
    {
        var request = new { account_name = accountName };
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/get_account",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var account = JsonSerializer.Deserialize<Account>(json, _jsonOptions);

        return account ?? throw new InvalidOperationException($"Account '{accountName}' not found");
    }

    /// <summary>
    /// Gets block information by height or ID
    /// </summary>
    public async Task<Block> GetBlockAsync(
        string blockNumOrId,
        CancellationToken cancellationToken = default
    )
    {
        var request = new { block_num_or_id = blockNumOrId };
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/get_block",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var block = JsonSerializer.Deserialize<Block>(json, _jsonOptions);

        return block ?? throw new InvalidOperationException($"Block '{blockNumOrId}' not found");
    }

    /// <summary>
    /// Pushes a signed transaction to the blockchain
    /// </summary>
    public async Task<TransactionResult> PushTransactionAsync(
        object signedTransaction,
        CancellationToken cancellationToken = default
    )
    {
        var requestContent = new StringContent(
            JsonSerializer.Serialize(signedTransaction, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/push_transaction",
            requestContent,
            cancellationToken
        );

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorInfo = "";
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (errorObj.TryGetProperty("error", out var error))
                {
                    errorInfo = error.ToString();
                }
            }
            catch
            {
                errorInfo = responseContent;
            }

            throw new InvalidOperationException($"Transaction failed: {errorInfo}");
        }

        var result = JsonSerializer.Deserialize<TransactionResult>(responseContent, _jsonOptions);
        return result
            ?? throw new InvalidOperationException("Failed to deserialize transaction result");
    }

    /// <summary>
    /// Gets table rows from a smart contract
    /// </summary>
    public async Task<TableRowsResult<T>> GetTableRowsAsync<T>(
        string contract,
        string scope,
        string table,
        CancellationToken cancellationToken = default
    )
    {
        var request = new
        {
            code = contract,
            scope = scope,
            table = table,
            json = true,
            limit = 1000,
        };

        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/get_table_rows",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TableRowsResult<T>>(json, _jsonOptions);

        return result ?? throw new InvalidOperationException("Failed to deserialize table rows");
    }

    /// <summary>
    /// Gets currency balance for an account
    /// </summary>
    public async Task<List<string>> GetCurrencyBalanceAsync(
        string contract,
        string account,
        string? symbol = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new
        {
            code = contract,
            account,
            symbol,
        };

        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/get_currency_balance",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var balances = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);

        return balances ?? new List<string>();
    }

    /// <summary>
    /// Gets the ABI for a smart contract
    /// </summary>
    public async Task<AbiDefinition?> GetAbiAsync(
        string contractAccount,
        CancellationToken cancellationToken = default
    )
    {
        var request = new { account_name = contractAccount };
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/get_abi",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GetAbiResponse>(json, _jsonOptions);

        return result?.Abi;
    }

    /// <summary>
    /// Converts JSON action data to binary using the chain's serialization
    /// </summary>
    public async Task<byte[]> AbiJsonToBinAsync(
        string contract,
        string action,
        object data,
        CancellationToken cancellationToken = default
    )
    {
        var request = new
        {
            code = contract,
            action = action,
            args = data,
        };

        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/abi_json_to_bin",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AbiJsonToBinResponse>(json, _jsonOptions);

        if (result == null || string.IsNullOrEmpty(result.BinArgs))
            return Array.Empty<byte>();

        // Convert hex string to bytes
        return Convert.FromHexString(result.BinArgs);
    }

    /// <summary>
    /// Converts binary action data back to JSON
    /// </summary>
    public async Task<object?> AbiBinToJsonAsync(
        string contract,
        string action,
        string binArgs,
        CancellationToken cancellationToken = default
    )
    {
        var request = new
        {
            code = contract,
            action = action,
            binargs = binArgs,
        };

        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "/v1/chain/abi_bin_to_json",
            requestContent,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AbiBinToJsonResponse>(json, _jsonOptions);

        return result?.Args;
    }

    /// <summary>
    /// Dispose the underlying HTTP client and release resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Generic POST request helper that returns a typed response
    /// </summary>
    public async Task<T?> PostJsonAsync<T>(
        string endpoint,
        object request,
        CancellationToken cancellationToken = default
    )
    {
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(endpoint, requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}

/// <summary>
/// Table rows query result
/// </summary>
public class TableRowsResult<T>
{
    /// <summary>
    /// Returned rows for the table query
    /// </summary>
    public List<T> Rows { get; set; } = new();

    /// <summary>
    /// True if there are more rows available
    /// </summary>
    public bool More { get; set; }

    /// <summary>
    /// Cursor/key for fetching the next set of rows
    /// </summary>
    public string? NextKey { get; set; }
}

/// <summary>
/// Transaction push result
/// </summary>
public class TransactionResult
{
    /// <summary>
    /// Transaction ID (hex)
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Processed transaction receipt
    /// </summary>
    public TransactionReceipt? Processed { get; set; }
}

/// <summary>
/// Transaction receipt details
/// </summary>
public class TransactionReceipt
{
    /// <summary>
    /// Transaction identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Block number where the transaction was included
    /// </summary>
    [JsonPropertyName("block_num")]
    public uint BlockNum { get; set; }
    
    /// <summary>
    /// Block time as string
    /// </summary>
    [JsonPropertyName("block_time")]
    public string BlockTime { get; set; } = string.Empty;
    
    /// <summary>
    /// Producer block id, if provided
    /// </summary>
    [JsonPropertyName("producer_block_id")]
    public string ProducerBlockId { get; set; } = string.Empty;

    /// <summary>
    /// Receipt header information
    /// </summary>
    public TransactionReceiptHeader? Receipt { get; set; }

    /// <summary>
    /// Elapsed time for processing
    /// </summary>
    public int Elapsed { get; set; }
    
    /// <summary>
    /// Net usage for the transaction
    /// </summary>
    [JsonPropertyName("net_usage")]
    public int NetUsage { get; set; }

    /// <summary>
    /// Whether the transaction was scheduled
    /// </summary>
    public bool Scheduled { get; set; }
    
    /// <summary>
    /// Action traces produced during processing
    /// </summary>
    [JsonPropertyName("action_traces")]
    public List<object>? ActionTraces { get; set; }
    
    /// <summary>
    /// RAM delta information per account
    /// </summary>
    [JsonPropertyName("account_ram_deltas")]
    public object? AccountRamDeltas { get; set; }

    /// <summary>
    /// Exception details, if any
    /// </summary>
    public object? Except { get; set; }
    
    /// <summary>
    /// Error code returned by the chain
    /// </summary>
    [JsonPropertyName("error_code")]
    public object? ErrorCode { get; set; }
}

/// <summary>
/// Transaction receipt header
/// </summary>
public class TransactionReceiptHeader
{
    /// <summary>
    /// Status of the receipt
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// CPU usage in microseconds
    /// </summary>
    public int CpuUsageUs { get; set; }

    /// <summary>
    /// Net usage in 64-bit words
    /// </summary>
    public int NetUsageWords { get; set; }
}
