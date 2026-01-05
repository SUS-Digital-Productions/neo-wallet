using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Services;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Transactions;

namespace SUS.EOS.Sharp.ESR;

/// <summary>
/// EOSIO Signing Request (ESR) protocol implementation
/// Based on eosio-signing-request specification
/// </summary>
public class EosioSigningRequest
{
    /// <summary>
    /// ESR protocol version
    /// </summary>
    public byte Version { get; set; } = 2;

    /// <summary>
    /// Chain ID or chain alias
    /// </summary>
    public string? ChainId { get; set; }

    /// <summary>
    /// Request payload (transaction or action)
    /// </summary>
    public EsrRequestPayload Payload { get; set; } = new();

    /// <summary>
    /// Request flags
    /// </summary>
    public EsrFlags Flags { get; set; } = EsrFlags.None;

    /// <summary>
    /// Callback URL
    /// </summary>
    public string? Callback { get; set; }

    /// <summary>
    /// Request info (metadata)
    /// </summary>
    public Dictionary<string, object>? Info { get; set; }

    /// <summary>
    /// Create ESR from URI (esr:// or web+esr://)
    /// </summary>
    public static EosioSigningRequest FromUri(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be empty", nameof(uri));

        // Remove protocol prefix
        var cleaned = uri
            .Replace("esr://", "")
            .Replace("web+esr://", "")
            .Replace("esr:", "");

        try
        {
            // Decode from base64url
            var decoded = Base64UrlDecode(cleaned);
            
            // Parse ESR data
            return ParseEsrData(decoded);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse ESR URI: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert ESR to URI format
    /// </summary>
    public string ToUri(string protocol = "esr")
    {
        var encoded = EncodeEsrData();
        return $"{protocol}://{Base64UrlEncode(encoded)}";
    }

    /// <summary>
    /// Sign the request and create callback response
    /// </summary>
    public EsrCallbackResponse Sign(string privateKeyWif, ChainInfo chainInfo)
    {
        try
        {
            // Check if this is an identity request (type 3)
            if (!Payload.IsTransaction && !Payload.IsAction)
            {
                // Identity request - no transaction to sign, just return identity proof
                // For identity requests, we just need to prove we control the private key
                // by providing the corresponding public key
                var key = EosioKey.FromPrivateKey(privateKeyWif);
                
                return new EsrCallbackResponse
                {
                    Signatures = new List<string>(),
                    ChainId = chainInfo.ChainId,
                    Signer = string.Empty, // Will be filled by caller
                    SignerPermission = string.Empty // Will be filled by caller
                };
            }

            // Build transaction from payload
            var transactionData = BuildTransaction(chainInfo);

            // Create transaction structure for signing
            // Parse action data from the transaction
            var actionArray = transactionData.GetType().GetProperty("actions")?.GetValue(transactionData) as Array;
            if (actionArray == null || actionArray.Length == 0)
                throw new InvalidOperationException("No actions in transaction");

            var firstAction = actionArray.GetValue(0);
            if (firstAction == null)
                throw new InvalidOperationException("First action is null");

            // Build proper transaction structure
            var builder = new EosioTransactionBuilder<byte[]>(chainInfo);
            builder.SetExpiration(TimeSpan.FromMinutes(5));

            // Extract action details
            var actionType = firstAction.GetType();
            var account = actionType.GetProperty("account")?.GetValue(firstAction) as string ?? throw new InvalidOperationException("Action account not found");
            var name = actionType.GetProperty("name")?.GetValue(firstAction) as string ?? throw new InvalidOperationException("Action name not found");
            var authorization = actionType.GetProperty("authorization")?.GetValue(firstAction);
            var data = actionType.GetProperty("data")?.GetValue(firstAction) as string ?? throw new InvalidOperationException("Action data not found");

            // Get actor and permission from authorization
            string actor = string.Empty;
            string permission = "active";
            if (authorization is Array authArray && authArray.Length > 0)
            {
                var auth = authArray.GetValue(0);
                if (auth != null)
                {
                    actor = auth.GetType().GetProperty("actor")?.GetValue(auth) as string ?? string.Empty;
                    permission = auth.GetType().GetProperty("permission")?.GetValue(auth) as string ?? "active";
                }
            }

            // Convert hex data to bytes
            var dataBytes = Convert.FromHexString(data);

            // Add action with binary data
            builder.AddActionWithBinaryData(account, name, actor, permission, dataBytes);
            var transaction = builder.Build();

            // Sign transaction properly
            var signer = new EosioSignatureProvider(privateKeyWif);
            var signature = signer.SignTransaction(chainInfo.ChainId, transaction);

            // Serialize transaction
            var serialized = EosioSerializer.SerializeTransactionWithBinaryData(transaction);
            var packedTrx = EosioSerializer.BytesToHexString(serialized);

            return new EsrCallbackResponse
            {
                Signatures = new List<string> { signature },
                Transaction = transactionData,
                SerializedTransaction = serialized,
                PackedTransaction = packedTrx,
                ChainId = chainInfo.ChainId
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign ESR: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Send callback response to callback URL
    /// </summary>
    public async Task<HttpResponseMessage> SendCallbackAsync(EsrCallbackResponse response, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(Callback))
            throw new InvalidOperationException("No callback URL specified");

        var client = httpClient ?? new HttpClient();
        
        try
        {
            var callbackData = new
            {
                tx = response.Transaction,
                sig = response.Signatures.First(),
                bn = response.BlockNum,
                bid = response.BlockId,
                sa = response.Signer,
                sp = response.SignerPermission,
                rbn = response.RefBlockNum,
                rid = response.RefBlockId,
                req = ToUri()
            };

            var content = new StringContent(
                JsonSerializer.Serialize(callbackData),
                Encoding.UTF8,
                "application/json"
            );

            return await client.PostAsync(Callback, content);
        }
        finally
        {
            if (httpClient == null)
                client.Dispose();
        }
    }

    /// <summary>
    /// Build transaction from ESR payload
    /// </summary>
    private object BuildTransaction(ChainInfo chainInfo)
    {
        // Build transaction based on payload type
        if (Payload.IsTransaction)
        {
            return Payload.Transaction ?? throw new InvalidOperationException("Transaction payload is null");
        }

        if (Payload.IsAction)
        {
            // Create transaction with single action
            var action = Payload.Action ?? throw new InvalidOperationException("Action payload is null");
            
            return new
            {
                expiration = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"),
                ref_block_num = chainInfo.LastIrreversibleBlockNum & 0xFFFF,
                ref_block_prefix = chainInfo.RefBlockPrefix,
                max_net_usage_words = 0,
                max_cpu_usage_ms = 0,
                delay_sec = 0,
                context_free_actions = Array.Empty<object>(),
                actions = new[] { action },
                transaction_extensions = Array.Empty<object>()
            };
        }

        throw new InvalidOperationException("Invalid payload type");
    }

    private static EosioSigningRequest ParseEsrData(byte[] data)
    {
        if (data.Length < 3)
            throw new InvalidOperationException($"ESR data too short: {data.Length} bytes");

        // First byte is the header containing version and flags
        var header = data[0];
        var version = (byte)(header & 0x07); // Lower 3 bits = version
        var isCompressed = (header & 0x80) != 0; // High bit = compression flag

        System.Diagnostics.Debug.WriteLine($"[ESR] Header: 0x{header:X2}, Version: {version}, Compressed: {isCompressed}");

        byte[] payload;
        if (isCompressed)
        {
            // Decompress raw deflate data (skip header byte)
            payload = DeflateDecompress(data.AsSpan(1).ToArray());
            System.Diagnostics.Debug.WriteLine($"[ESR] Decompressed {data.Length - 1} bytes to {payload.Length} bytes");
        }
        else
        {
            // Uncompressed data (skip header byte)
            payload = data.AsSpan(1).ToArray();
        }

        // Parse the binary ESR payload
        return ParseBinaryEsr(payload, version);
    }

    private static byte[] DeflateDecompress(byte[] compressedData)
    {
        // ESR uses raw DEFLATE compression (RFC 1951), not zlib (RFC 1950)
        // So we should NOT skip any header bytes
        if (compressedData.Length < 1)
            throw new InvalidOperationException($"Compressed data too short: {compressedData.Length} bytes");

        System.Diagnostics.Debug.WriteLine($"[ESR] First bytes: 0x{compressedData[0]:X2} 0x{(compressedData.Length > 1 ? compressedData[1].ToString("X2") : "NA")}");

        using var inputStream = new MemoryStream(compressedData);
        using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        
        deflateStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private static EosioSigningRequest ParseBinaryEsr(byte[] data, byte version)
    {
        var request = new EosioSigningRequest { Version = version };
        var offset = 0;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsing binary ESR, {data.Length} bytes");
            
            // Read chain_id (variant: [type, data])
            if (offset >= data.Length)
                throw new InvalidOperationException("Unexpected end of data reading chain_id type");
            var chainIdType = data[offset++];
            System.Diagnostics.Debug.WriteLine($"[ESR] Chain ID type: {chainIdType}");
            
            if (chainIdType == 0)
            {
                // Chain alias (uint8)
                if (offset >= data.Length)
                    throw new InvalidOperationException("Unexpected end of data reading chain alias");
                var alias = data[offset++];
                request.ChainId = GetChainIdFromAlias(alias);
                System.Diagnostics.Debug.WriteLine($"[ESR] Chain alias: {alias} -> {request.ChainId?[..16]}...");
            }
            else if (chainIdType == 1)
            {
                // Full chain ID (32 bytes)
                if (offset + 32 > data.Length)
                    throw new InvalidOperationException($"Not enough data for chain ID: need 32 bytes, have {data.Length - offset}");
                var chainIdBytes = data.AsSpan(offset, 32).ToArray();
                request.ChainId = Convert.ToHexString(chainIdBytes).ToLowerInvariant();
                offset += 32;
                System.Diagnostics.Debug.WriteLine($"[ESR] Full chain ID: {request.ChainId[..16]}...");
            }
            else
            {
                throw new InvalidOperationException($"Unknown chain_id type: {chainIdType}");
            }

            // Read request type (variant: [type, data])
            if (offset >= data.Length)
                throw new InvalidOperationException("Unexpected end of data reading request type");
            var requestType = data[offset++];
            System.Diagnostics.Debug.WriteLine($"[ESR] Request type: {requestType}");
            
            if (requestType == 0)
            {
                // Single action
                var action = ParseAction(data, ref offset);
                request.Payload = new EsrRequestPayload { Action = action };
            }
            else if (requestType == 1)
            {
                // Multiple actions (array)
                var actionCount = ReadVarUint(data, ref offset);
                System.Diagnostics.Debug.WriteLine($"[ESR] Action count: {actionCount}");
                var actions = new List<object>();
                for (var i = 0; i < actionCount; i++)
                {
                    actions.Add(ParseAction(data, ref offset));
                }
                request.Payload = new EsrRequestPayload 
                { 
                    Transaction = new { actions = actions }
                };
            }
            else if (requestType == 2)
            {
                // Full transaction
                var transaction = ParseTransaction(data, ref offset);
                request.Payload = new EsrRequestPayload { Transaction = transaction };
            }
            else if (requestType == 3)
            {
                // Identity request (no payload)
                request.Payload = new EsrRequestPayload();
            }
            else
            {
                throw new InvalidOperationException($"Unknown request type: {requestType}");
            }

            // Read flags
            if (offset < data.Length)
            {
                request.Flags = (EsrFlags)data[offset++];
            }

            // Read callback
            if (offset < data.Length)
            {
                var callbackLength = ReadVarUint(data, ref offset);
                if (callbackLength > 0 && offset + (int)callbackLength <= data.Length)
                {
                    request.Callback = Encoding.UTF8.GetString(data, offset, (int)callbackLength);
                    offset += (int)callbackLength;
                }
            }

            // Read info pairs (optional)
            if (offset < data.Length)
            {
                var infoCount = ReadVarUint(data, ref offset);
                if (infoCount > 0)
                {
                    request.Info = new Dictionary<string, object>();
                    for (var i = 0; i < infoCount && offset < data.Length; i++)
                    {
                        var keyLength = ReadVarUint(data, ref offset);
                        if (offset + (int)keyLength > data.Length) break;
                        var key = Encoding.UTF8.GetString(data, offset, (int)keyLength);
                        offset += (int)keyLength;
                        
                        var valueLength = ReadVarUint(data, ref offset);
                        if (offset + (int)valueLength > data.Length) break;
                        var value = Encoding.UTF8.GetString(data, offset, (int)valueLength);
                        offset += (int)valueLength;
                        
                        request.Info[key] = value;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ESR] Successfully parsed ESR request");
            return request;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ESR] Parse error at offset {offset}: {ex.Message}");
            throw;
        }
    }

    private static object ParseAction(byte[] data, ref int offset)
    {
        // Read account name (8 bytes, name encoded)
        if (offset + 8 > data.Length)
            throw new InvalidOperationException($"Not enough data for account name at offset {offset}");
        var accountBytes = data.AsSpan(offset, 8).ToArray();
        var account = DecodeName(accountBytes);
        offset += 8;
        System.Diagnostics.Debug.WriteLine($"[ESR] Action account: {account}");

        // Read action name (8 bytes, name encoded)
        if (offset + 8 > data.Length)
            throw new InvalidOperationException($"Not enough data for action name at offset {offset}");
        var nameBytes = data.AsSpan(offset, 8).ToArray();
        var name = DecodeName(nameBytes);
        offset += 8;
        System.Diagnostics.Debug.WriteLine($"[ESR] Action name: {name}");

        // Read authorization array
        var authCount = ReadVarUint(data, ref offset);
        System.Diagnostics.Debug.WriteLine($"[ESR] Auth count: {authCount}");
        var authorizations = new List<object>();
        for (var i = 0; i < authCount; i++)
        {
            if (offset + 16 > data.Length)
                throw new InvalidOperationException($"Not enough data for authorization at offset {offset}");
            
            var actorBytes = data.AsSpan(offset, 8).ToArray();
            var actor = DecodeName(actorBytes);
            offset += 8;

            var permBytes = data.AsSpan(offset, 8).ToArray();
            var permission = DecodeName(permBytes);
            offset += 8;

            authorizations.Add(new { actor, permission });
            System.Diagnostics.Debug.WriteLine($"[ESR] Auth: {actor}@{permission}");
        }

        // Read data (bytes)
        var dataLength = ReadVarUint(data, ref offset);
        if (offset + (int)dataLength > data.Length)
            throw new InvalidOperationException($"Not enough data for action data: need {dataLength} bytes, have {data.Length - offset}");
        var actionData = Convert.ToHexString(data.AsSpan(offset, (int)dataLength).ToArray()).ToLowerInvariant();
        offset += (int)dataLength;
        System.Diagnostics.Debug.WriteLine($"[ESR] Action data length: {dataLength}");

        return new
        {
            account,
            name,
            authorization = authorizations,
            data = actionData
        };
    }

    private static object ParseTransaction(byte[] data, ref int offset)
    {
        // Read transaction header
        var expiration = BitConverter.ToUInt32(data, offset);
        offset += 4;
        
        var refBlockNum = BitConverter.ToUInt16(data, offset);
        offset += 2;
        
        var refBlockPrefix = BitConverter.ToUInt32(data, offset);
        offset += 4;

        var maxNetUsageWords = ReadVarUint(data, ref offset);
        var maxCpuUsageMs = data[offset++];
        var delaySec = ReadVarUint(data, ref offset);

        // Context-free actions
        var cfaCount = ReadVarUint(data, ref offset);
        var contextFreeActions = new List<object>();
        for (var i = 0; i < cfaCount; i++)
        {
            contextFreeActions.Add(ParseAction(data, ref offset));
        }

        // Actions
        var actionCount = ReadVarUint(data, ref offset);
        var actions = new List<object>();
        for (var i = 0; i < actionCount; i++)
        {
            actions.Add(ParseAction(data, ref offset));
        }

        // Transaction extensions
        var extCount = ReadVarUint(data, ref offset);
        var extensions = new List<object>();
        for (var i = 0; i < extCount; i++)
        {
            var extType = BitConverter.ToUInt16(data, offset);
            offset += 2;
            var extDataLen = ReadVarUint(data, ref offset);
            var extData = Convert.ToHexString(data.AsSpan(offset, (int)extDataLen).ToArray());
            offset += (int)extDataLen;
            extensions.Add(new { type = extType, data = extData });
        }

        // Convert expiration to ISO string
        var expirationDate = DateTimeOffset.FromUnixTimeSeconds(expiration).UtcDateTime;

        return new
        {
            expiration = expirationDate.ToString("yyyy-MM-ddTHH:mm:ss"),
            ref_block_num = refBlockNum,
            ref_block_prefix = refBlockPrefix,
            max_net_usage_words = maxNetUsageWords,
            max_cpu_usage_ms = maxCpuUsageMs,
            delay_sec = delaySec,
            context_free_actions = contextFreeActions,
            actions = actions,
            transaction_extensions = extensions
        };
    }

    private static uint ReadVarUint(byte[] data, ref int offset)
    {
        uint result = 0;
        var shift = 0;
        
        while (offset < data.Length)
        {
            var b = data[offset++];
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        
        return result;
    }

    private static string DecodeName(byte[] bytes)
    {
        const string charmap = ".12345abcdefghijklmnopqrstuvwxyz";
        
        // EOSIO names are stored as 64-bit little-endian integers
        var value = BitConverter.ToUInt64(bytes, 0);
        
        if (value == 0)
            return "";

        var result = new StringBuilder(13);
        
        // EOSIO name encoding: 12 characters of 5 bits each + 1 character of 4 bits
        for (var i = 0; i < 13; i++)
        {
            int c;
            if (i == 12)
            {
                // Last character uses only 4 bits
                c = (int)(value & 0x0F);
            }
            else
            {
                // First 12 characters use 5 bits each, starting from high bits
                c = (int)((value >> (64 - 5 * (i + 1))) & 0x1F);
            }
            
            if (c < charmap.Length)
            {
                result.Append(charmap[c]);
            }
        }
        
        // Trim trailing dots
        return result.ToString().TrimEnd('.');
    }

    private static string GetChainIdFromAlias(byte alias)
    {
        // Common chain aliases from ESR spec
        // See: https://github.com/greymass/eosio-signing-request/blob/master/src/signing-request.ts
        return alias switch
        {
            1 => "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", // EOS Mainnet
            2 => "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", // Telos
            3 => "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", // WAX
            4 => "384da888112027f0321850a169f737c33e53b388aad48b5adace4bab97f437e0", // Proton
            5 => "e70aaab8997e1dfce58fbfac80cbbb8fecec7b99cf982a9444273cbc64c41473", // FIO
            6 => "73e4385a2708e6d7048834fbc1079f2fabb17b3c125b146af438971e90716c4d", // Jungle Testnet
            7 => "b64646740308df2ee06c6b72f34c0f7fa066d940e831f752db2006fcc2b78dee", // Kylin Testnet  
            8 => "f16b1833c747c43682f4386fca9cbb327929334a762755ebec17f6f23c9b8a12", // Libre
            9 => "b20901380af44ef59c5918439a1f9a41d83669020319a80574b804a5f95cbd7e", // UX Network
            10 => "8fc6dce7942189f842170de953932b1f66693ad3788f766e777b6f9d22335c02", // Jungle4 Testnet
            _ => throw new InvalidOperationException($"Unknown chain alias: {alias}")
        };
    }

    private byte[] EncodeEsrData()
    {
        var json = JsonSerializer.Serialize(this);
        return Encoding.UTF8.GetBytes(json);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string encoded)
    {
        var base64 = encoded
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}

/// <summary>
/// ESR request payload (transaction or action)
/// </summary>
public class EsrRequestPayload
{
    public bool IsTransaction => Transaction != null;
    public bool IsAction => Action != null;

    public object? Transaction { get; set; }
    public object? Action { get; set; }
}

/// <summary>
/// ESR request flags
/// </summary>
[Flags]
public enum EsrFlags : byte
{
    None = 0,
    Broadcast = 1 << 0,      // Request should be broadcast by wallet
    Background = 1 << 1       // Request can be processed in background
}

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
    public string? Signer { get; set; }
    public string? SignerPermission { get; set; }
    public uint? RefBlockNum { get; set; }
    public string? RefBlockId { get; set; }
}

/// <summary>
/// ESR service for handling signing requests
/// </summary>
public interface IEsrService
{
    /// <summary>
    /// Parse ESR from URI
    /// </summary>
    Task<EosioSigningRequest> ParseRequestAsync(string uri);

    /// <summary>
    /// Sign ESR and return response (requires blockchain client for chain info and optional broadcasting)
    /// </summary>
    Task<EsrCallbackResponse> SignRequestAsync(EosioSigningRequest request, string privateKeyWif, IAntelopeBlockchainClient? blockchainClient = null, bool broadcast = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sign and broadcast transaction
    /// </summary>
    Task<EsrCallbackResponse> SignAndBroadcastAsync(EosioSigningRequest request, string privateKeyWif, IAntelopeBlockchainClient blockchainClient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send callback response
    /// </summary>
    Task<bool> SendCallbackAsync(EosioSigningRequest request, EsrCallbackResponse response);
}

/// <summary>
/// ESR service implementation
/// </summary>
public class EsrService : IEsrService
{
    private readonly HttpClient _httpClient;

    public EsrService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task<EosioSigningRequest> ParseRequestAsync(string uri)
    {
        try
        {
            var request = EosioSigningRequest.FromUri(uri);
            return Task.FromResult(request);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse ESR: {ex.Message}", ex);
        }
    }

    public async Task<EsrCallbackResponse> SignRequestAsync(
        EosioSigningRequest request, 
        string privateKeyWif, 
        IAntelopeBlockchainClient? blockchainClient = null,
        bool broadcast = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ChainInfo chainInfo;
            
            if (blockchainClient != null)
            {
                // Get real chain info from blockchain
                chainInfo = await blockchainClient.GetInfoAsync(cancellationToken);
            }
            else
            {
                // Fallback to dummy chain info (won't work for real transactions)
                var chainId = request.ChainId ?? throw new InvalidOperationException("Chain ID not specified");
                chainInfo = new ChainInfo
                {
                    ChainId = chainId,
                    ServerVersion = "unknown",
                    HeadBlockNum = 0,
                    LastIrreversibleBlockNum = 0,
                    LastIrreversibleBlockId = string.Empty,
                    HeadBlockId = string.Empty,
                    HeadBlockTime = DateTime.UtcNow,
                    HeadBlockProducer = string.Empty,
                    VirtualBlockCpuLimit = 0,
                    VirtualBlockNetLimit = 0,
                    BlockCpuLimit = 0,
                    BlockNetLimit = 0,
                    RefBlockPrefix = 0
                };
            }

            var response = request.Sign(privateKeyWif, chainInfo);

            // Broadcast if requested or if Broadcast flag is set in ESR
            if (broadcast || request.Flags.HasFlag(EsrFlags.Broadcast))
            {
                if (blockchainClient == null)
                    throw new InvalidOperationException("Blockchain client required for broadcasting");

                // Only broadcast if there's an actual transaction (not identity requests)
                if (response.SerializedTransaction != null && response.SerializedTransaction.Length > 0)
                {
                    var pushRequest = new
                    {
                        signatures = response.Signatures,
                        compression = 0,
                        packed_context_free_data = "",
                        packed_trx = response.PackedTransaction
                    };

                    var result = await blockchainClient.PushTransactionAsync(pushRequest, cancellationToken);
                    
                    // Update response with blockchain data
                    if (result.Processed != null)
                    {
                        response.BlockNum = (uint?)result.Processed.GetType().GetProperty("BlockNum")?.GetValue(result.Processed);
                        response.BlockId = result.Processed.GetType().GetProperty("BlockId")?.GetValue(result.Processed) as string;
                    }
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign ESR: {ex.Message}", ex);
        }
    }

    public async Task<EsrCallbackResponse> SignAndBroadcastAsync(
        EosioSigningRequest request, 
        string privateKeyWif, 
        IAntelopeBlockchainClient blockchainClient,
        CancellationToken cancellationToken = default)
    {
        return await SignRequestAsync(request, privateKeyWif, blockchainClient, broadcast: true, cancellationToken);
    }

    public async Task<bool> SendCallbackAsync(EosioSigningRequest request, EsrCallbackResponse response)
    {
        try
        {
            var httpResponse = await request.SendCallbackAsync(response, _httpClient);
            return httpResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}