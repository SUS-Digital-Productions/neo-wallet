using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Transactions;

namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// EOSIO Signing Request (ESR) protocol implementation
/// Based on eosio-signing-request specification
/// </summary>
public class Esr
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
    /// Original ESR URI (preserved for callbacks)
    /// </summary>
    public string? OriginalUri { get; set; }

    /// <summary>
    /// Create ESR from URI (esr:// or web+esr://)
    /// </summary>
    public static Esr FromUri(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be empty", nameof(uri));

        System.Diagnostics.Debug.WriteLine($"[ESR] Parsing URI: {uri}");
        
        // Remove protocol prefix
        var cleaned = uri.Replace("esr://", "").Replace("web+esr://", "").Replace("esr:", "");
        System.Diagnostics.Debug.WriteLine($"[ESR] Cleaned payload (first 100 chars): {(cleaned.Length > 100 ? cleaned[..100] + "..." : cleaned)}");

        try
        {
            // Decode from base64url
            var decoded = Base64UrlDecode(cleaned);
            System.Diagnostics.Debug.WriteLine($"[ESR] Decoded data: {decoded.Length} bytes");
            
            // Print hex dump of first 64 bytes for debugging
            var hexDump = Convert.ToHexString(decoded.Take(Math.Min(64, decoded.Length)).ToArray());
            System.Diagnostics.Debug.WriteLine($"[ESR] First 64 bytes (hex): {hexDump}");

            // Parse ESR data and preserve original URI
            var esr = ParseEsrData(decoded);
            esr.OriginalUri = uri;
            return esr;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse ESR URI: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert ESR to URI format. Returns original URI if available.
    /// </summary>
    public string ToUri(string protocol = "esr")
    {
        // Return original URI if we have it (preserves exact encoding)
        if (!string.IsNullOrEmpty(OriginalUri))
        {
            return OriginalUri;
        }
        
        // Fall back to re-encoding (may not be compatible)
        var encoded = EncodeEsrData();
        return $"{protocol}://{Base64UrlEncode(encoded)}";
    }

    /// <summary>
    /// Sign the request and create callback response
    /// </summary>
    /// <param name="privateKeyWif">Private key in WIF format</param>
    /// <param name="chainInfo">Chain info for TAPOS</param>
    /// <param name="signer">Account name to use for signing (resolves placeholders)</param>
    /// <param name="signerPermission">Permission to use for signing (resolves placeholders)</param>
    public EsrCallbackResponse Sign(string privateKeyWif, ChainInfo chainInfo, string signer, string signerPermission)
    {
        try
        {
            // Check if this is an identity request (type 3)
            if (!Payload.IsTransaction && !Payload.IsAction)
            {
                System.Diagnostics.Trace.WriteLine("[ESR] Processing identity request - creating identity proof");
                
                // Identity request - we need to create an identity proof
                // This involves signing a message that proves we control the key
                var key = EosioKey.FromPrivateKey(privateKeyWif);
                System.Diagnostics.Trace.WriteLine($"[ESR] Public key: {key.PublicKey}");
                
                // For identity proofs, we create a minimal transaction with an identity action
                // The signer and expiration will be set by the caller
                // We need to sign a digest that proves key ownership
                
                // Get scope from info if available
                var scope = Info?.ContainsKey("scope") == true ? Info["scope"]?.ToString() ?? "" : "";
                System.Diagnostics.Trace.WriteLine($"[ESR] Scope: {scope}");
                
                // Create expiration timestamp
                var expiration = DateTime.UtcNow.AddMinutes(5);
                var expirationStr = expiration.ToString("yyyy-MM-ddTHH:mm:ss");
                System.Diagnostics.Trace.WriteLine($"[ESR] Expiration: {expirationStr}");
                
                // Build identity proof data to sign
                // Format: chain_id + transaction header + identity action
                var proofData = new List<byte>();
                
                // Add chain ID (32 bytes hex decoded)
                if (!string.IsNullOrEmpty(chainInfo.ChainId))
                {
                    proofData.AddRange(Convert.FromHexString(chainInfo.ChainId));
                    System.Diagnostics.Trace.WriteLine($"[ESR] Chain ID: {chainInfo.ChainId}");
                }
                
                // Add zeros for ref_block_num (2 bytes) and ref_block_prefix (4 bytes) 
                proofData.AddRange(new byte[] { 0, 0 }); // ref_block_num
                proofData.AddRange(new byte[] { 0, 0, 0, 0 }); // ref_block_prefix
                
                // Add expiration (4 bytes)
                var expSeconds = (uint)((expiration - new DateTime(1970, 1, 1)).TotalSeconds);
                proofData.AddRange(BitConverter.GetBytes(expSeconds));
                System.Diagnostics.Trace.WriteLine($"[ESR] Expiration timestamp: {expSeconds}");
                
                // Add empty context_free_actions count (varuint = 0)
                proofData.Add(0);
                
                // Add actions count (varuint = 1)
                proofData.Add(1);
                
                // Add identity action
                // account = 0 (8 bytes)
                proofData.AddRange(new byte[8]);
                // name = "identity" encoded as 8 bytes
                proofData.AddRange(EncodeNameToBytes("identity"));
                // authorization count = 0 
                proofData.Add(0);
                // data length = 0
                proofData.Add(0);
                
                // Add empty transaction_extensions count
                proofData.Add(0);
                
                // Add 32 zero bytes (context free data hash)
                proofData.AddRange(new byte[32]);
                
                System.Diagnostics.Trace.WriteLine($"[ESR] Proof data length: {proofData.Count} bytes");
                System.Diagnostics.Trace.WriteLine($"[ESR] Proof data hex: {Convert.ToHexString(proofData.ToArray())}");
                
                // Compute SHA256 digest and sign
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var digest = sha256.ComputeHash(proofData.ToArray());
                System.Diagnostics.Trace.WriteLine($"[ESR] Digest: {Convert.ToHexString(digest)}");
                
                // Sign the digest
                var identitySigner = new EosioSignatureProvider(privateKeyWif);
                var identitySignature = identitySigner.SignDigest(digest);
                System.Diagnostics.Trace.WriteLine($"[ESR] Identity signature: {identitySignature}");

                return new EsrCallbackResponse
                {
                    Signatures = new List<string> { identitySignature },
                    ChainId = chainInfo.ChainId,
                    Signer = string.Empty, // Will be filled by caller
                    SignerPermission = string.Empty, // Will be filled by caller
                    RefBlockNum = 0,
                    RefBlockId = "0",
                };
            }

            // Build transaction from payload
            var transactionData = BuildTransaction(chainInfo);

            // Create transaction structure for signing
            // Parse action data from the transaction
            var actionArray =
                transactionData.GetType().GetProperty("actions")?.GetValue(transactionData)
                as Array;
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
            var account =
                actionType.GetProperty("account")?.GetValue(firstAction) as string
                ?? throw new InvalidOperationException("Action account not found");
            var name =
                actionType.GetProperty("name")?.GetValue(firstAction) as string
                ?? throw new InvalidOperationException("Action name not found");
            var data =
                actionType.GetProperty("data")?.GetValue(firstAction) as string
                ?? throw new InvalidOperationException("Action data not found");

            // Use signer and signerPermission passed in to resolve placeholders
            // ESR uses placeholder values like "............1" that must be replaced
            System.Diagnostics.Trace.WriteLine($"[ESR] Using signer: {signer}@{signerPermission}");

            // Convert hex data to bytes
            var dataBytes = Convert.FromHexString(data);
            System.Diagnostics.Trace.WriteLine($"[ESR] Action data hex: {data}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Action data bytes: {dataBytes.Length}");

            // Add action with binary data - use signer parameters (not placeholders from ESR)
            builder.AddActionWithBinaryData(account, name, signer, signerPermission, dataBytes);
            var transaction = builder.Build();
            
            System.Diagnostics.Trace.WriteLine($"[ESR] Transaction RefBlockNum: {transaction.RefBlockNum}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Transaction RefBlockPrefix: {transaction.RefBlockPrefix}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Transaction Expiration: {transaction.Expiration}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Transaction Actions Count: {transaction.Actions.Count}");
            if (transaction.Actions.Count > 0)
            {
                var firstTxAction = transaction.Actions[0];
                System.Diagnostics.Trace.WriteLine($"[ESR] First action: {firstTxAction.Account}::{firstTxAction.Name}");
                System.Diagnostics.Trace.WriteLine($"[ESR] Authorization count: {firstTxAction.Authorization.Count}");
                foreach (var auth in firstTxAction.Authorization)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Authorization: {auth.Actor}@{auth.Permission}");
                }
            }

            // Sign transaction properly
            var signatureProvider = new EosioSignatureProvider(privateKeyWif);
            var signature = signatureProvider.SignTransaction(chainInfo.ChainId, transaction);
            System.Diagnostics.Trace.WriteLine($"[ESR] Signature: {signature}");

            // Serialize transaction
            var serialized = EosioSerializer.SerializeTransactionWithBinaryData(transaction);
            var packedTrx = EosioSerializer.BytesToHexString(serialized);
            System.Diagnostics.Trace.WriteLine($"[ESR] Packed transaction: {packedTrx}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Serialized length: {serialized.Length} bytes");

            return new EsrCallbackResponse
            {
                Signatures = new List<string> { signature },
                Transaction = transactionData,
                SerializedTransaction = serialized,
                PackedTransaction = packedTrx,
                ChainId = chainInfo.ChainId,
                // Include TAPOS values for callback validation
                RefBlockNum = (uint)(chainInfo.LastIrreversibleBlockNum & 0xFFFF),
                RefBlockPrefix = chainInfo.RefBlockPrefix,
                RefBlockId = chainInfo.LastIrreversibleBlockId,
                Signer = signer,
                SignerPermission = signerPermission,
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
    public async Task<HttpResponseMessage> SendCallbackAsync(
        EsrCallbackResponse response,
        HttpClient? httpClient = null
    )
    {
        if (string.IsNullOrEmpty(Callback))
            throw new InvalidOperationException("No callback URL specified");

        var client = httpClient ?? new HttpClient();

        try
        {
            // ESR callback format per Anchor/greymass spec
            // Only include required fields - extra fields can cause parsing issues
            var callbackData = new
            {
                sig = response.Signatures.First(),              // Signature
                tx = response.PackedTransaction,                // Packed transaction hex
                sa = response.Signer,                           // Signer actor
                sp = response.SignerPermission,                 // Signer permission
                rbn = response.RefBlockNum?.ToString(),         // Ref block num as string
                rid = response.RefBlockNum?.ToString(),         // Ref block ID (use block num, not full ID)
                ex = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"), // Expiration
                req = ToUri(),                                  // Original request
                cid = response.ChainId,                         // Chain ID
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
        // ALWAYS rebuild transaction with fresh block references from chain
        // The ESR may contain stale ref_block_num/ref_block_prefix that are no longer valid
        
        object[]? actions = null;
        
        if (Payload.IsTransaction)
        {
            // Extract actions from the existing transaction
            var tx = Payload.Transaction
                ?? throw new InvalidOperationException("Transaction payload is null");
            
            // Get actions array from transaction object
            var actionsProperty = tx.GetType().GetProperty("actions");
            if (actionsProperty == null)
            {
                // Try as dictionary
                if (tx is IDictionary<string, object> dict && dict.TryGetValue("actions", out var actionsObj))
                {
                    actions = actionsObj as object[];
                }
                else if (tx is System.Text.Json.JsonElement jsonElement)
                {
                    if (jsonElement.TryGetProperty("actions", out var actionsElement))
                    {
                        var actionsList = new List<object>();
                        foreach (var actionEl in actionsElement.EnumerateArray())
                        {
                            actionsList.Add(actionEl);
                        }
                        actions = actionsList.ToArray();
                    }
                }
            }
            else
            {
                actions = actionsProperty.GetValue(tx) as object[];
            }
            
            if (actions == null || actions.Length == 0)
                throw new InvalidOperationException("No actions found in transaction");
        }
        else if (Payload.IsAction)
        {
            // Single action
            var action = Payload.Action 
                ?? throw new InvalidOperationException("Action payload is null");
            actions = new[] { action };
        }
        else
        {
            throw new InvalidOperationException("Invalid payload type");
        }

        // Build NEW transaction with FRESH block references from chainInfo
        System.Diagnostics.Debug.WriteLine($"[ESR] Building transaction with fresh block refs: block_num={chainInfo.LastIrreversibleBlockNum}, prefix={chainInfo.RefBlockPrefix}");
        
        return new
        {
            expiration = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"),
            ref_block_num = chainInfo.LastIrreversibleBlockNum & 0xFFFF,
            ref_block_prefix = chainInfo.RefBlockPrefix,
            max_net_usage_words = 0,
            max_cpu_usage_ms = 0,
            delay_sec = 0,
            context_free_actions = Array.Empty<object>(),
            actions = actions,
            transaction_extensions = Array.Empty<object>(),
        };
    }

    private static Esr ParseEsrData(byte[] data)
    {
        if (data.Length < 3)
            throw new InvalidOperationException($"ESR data too short: {data.Length} bytes");

        // First byte is the header containing version and flags
        var header = data[0];
        var version = (byte)(header & 0x07); // Lower 3 bits = version
        var isCompressed = (header & 0x80) != 0; // High bit = compression flag

        System.Diagnostics.Debug.WriteLine(
            $"[ESR] Header: 0x{header:X2}, Version: {version}, Compressed: {isCompressed}"
        );

        byte[] payload;
        if (isCompressed)
        {
            // Decompress raw deflate data (skip header byte)
            payload = DeflateDecompress(data.AsSpan(1).ToArray());
            System.Diagnostics.Debug.WriteLine(
                $"[ESR] Decompressed {data.Length - 1} bytes to {payload.Length} bytes"
            );
            
            // Print hex dump of decompressed payload for debugging
            var hexDump = Convert.ToHexString(payload.Take(Math.Min(128, payload.Length)).ToArray());
            System.Diagnostics.Debug.WriteLine($"[ESR] Decompressed first 128 bytes (hex): {hexDump}");
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
            throw new InvalidOperationException(
                $"Compressed data too short: {compressedData.Length} bytes"
            );

        System.Diagnostics.Debug.WriteLine(
            $"[ESR] First bytes: 0x{compressedData[0]:X2} 0x{(compressedData.Length > 1 ? compressedData[1].ToString("X2") : "NA")}"
        );

        using var inputStream = new MemoryStream(compressedData);
        using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();

        deflateStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private static Esr ParseBinaryEsr(byte[] data, byte version)
    {
        var request = new Esr { Version = version };
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
                    throw new InvalidOperationException(
                        "Unexpected end of data reading chain alias"
                    );
                var alias = data[offset++];
                request.ChainId = GetChainIdFromAlias(alias);
                System.Diagnostics.Debug.WriteLine(
                    $"[ESR] Chain alias: {alias} -> {request.ChainId?[..16]}..."
                );
            }
            else if (chainIdType == 1)
            {
                // Full chain ID (32 bytes)
                if (offset + 32 > data.Length)
                    throw new InvalidOperationException(
                        $"Not enough data for chain ID: need 32 bytes, have {data.Length - offset}"
                    );
                var chainIdBytes = data.AsSpan(offset, 32).ToArray();
                request.ChainId = Convert.ToHexString(chainIdBytes).ToLowerInvariant();
                offset += 32;
                System.Diagnostics.Debug.WriteLine(
                    $"[ESR] Full chain ID: {request.ChainId[..16]}..."
                );
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
                request.Payload = new EsrRequestPayload { Transaction = new { actions = actions } };
            }
            else if (requestType == 2)
            {
                // Full transaction
                var transaction = ParseTransaction(data, ref offset);
                request.Payload = new EsrRequestPayload { Transaction = transaction };
            }
            else if (requestType == 3)
            {
                // Identity request - ESR v3 has identity data (scope + optional permission)
                // IdentityV3 structure: scope (8 bytes Name) + permission (optional PermissionLevel - 16 bytes if present)
                System.Diagnostics.Debug.WriteLine($"[ESR] Parsing identity request at offset {offset}");
                
                // For ESR v3 identity, we need to read the identity data
                if (version >= 3 && offset + 8 <= data.Length)
                {
                    // Read scope (8 bytes, name encoded)
                    var scopeBytes = data.AsSpan(offset, 8).ToArray();
                    var scope = DecodeName(scopeBytes);
                    offset += 8;
                    System.Diagnostics.Debug.WriteLine($"[ESR] Identity scope: {scope}");
                    
                    // Check if there's a permission level (optional)
                    // We need to check if there's more data and if the next byte could be actor/permission
                    // In ESR binary format, optional fields are preceded by a presence byte (0 or 1)
                    if (offset < data.Length)
                    {
                        var hasPermission = data[offset++];
                        System.Diagnostics.Debug.WriteLine($"[ESR] Identity has permission flag: {hasPermission}");
                        
                        if (hasPermission == 1 && offset + 16 <= data.Length)
                        {
                            var actorBytes = data.AsSpan(offset, 8).ToArray();
                            var actor = DecodeName(actorBytes);
                            offset += 8;
                            
                            var permBytes = data.AsSpan(offset, 8).ToArray();
                            var permission = DecodeName(permBytes);
                            offset += 8;
                            
                            System.Diagnostics.Debug.WriteLine($"[ESR] Identity permission: {actor}@{permission}");
                        }
                    }
                }
                else if (version == 2)
                {
                    // ESR v2 identity: optional permission level only
                    if (offset < data.Length)
                    {
                        var hasPermission = data[offset++];
                        System.Diagnostics.Debug.WriteLine($"[ESR] Identity v2 has permission flag: {hasPermission}");
                        
                        if (hasPermission == 1 && offset + 16 <= data.Length)
                        {
                            var actorBytes = data.AsSpan(offset, 8).ToArray();
                            var actor = DecodeName(actorBytes);
                            offset += 8;
                            
                            var permBytes = data.AsSpan(offset, 8).ToArray();
                            var permission = DecodeName(permBytes);
                            offset += 8;
                            
                            System.Diagnostics.Debug.WriteLine($"[ESR] Identity v2 permission: {actor}@{permission}");
                        }
                    }
                }
                
                request.Payload = new EsrRequestPayload();
                System.Diagnostics.Debug.WriteLine($"[ESR] After identity parsing, offset: {offset}");
            }
            else
            {
                throw new InvalidOperationException($"Unknown request type: {requestType}");
            }

            // Read flags
            if (offset < data.Length)
            {
                request.Flags = (EsrFlags)data[offset++];
                System.Diagnostics.Debug.WriteLine($"[ESR] Flags: {request.Flags} (0x{(byte)request.Flags:X2})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ESR] No flags byte - end of data");
            }

            // Read callback
            System.Diagnostics.Debug.WriteLine($"[ESR] Reading callback at offset {offset}, data length: {data.Length}");
            if (offset < data.Length)
            {
                var callbackLengthOffset = offset;
                var callbackLength = ReadVarUint(data, ref offset);
                System.Diagnostics.Debug.WriteLine($"[ESR] Callback length VarUint at {callbackLengthOffset}: {callbackLength} (consumed {offset - callbackLengthOffset} bytes)");
                
                if (callbackLength > 0)
                {
                    if (offset + (int)callbackLength <= data.Length)
                    {
                        request.Callback = Encoding.UTF8.GetString(data, offset, (int)callbackLength);
                        System.Diagnostics.Debug.WriteLine($"[ESR] Callback URL: {request.Callback}");
                        offset += (int)callbackLength;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ESR] Warning: Callback length {callbackLength} exceeds remaining data ({data.Length - offset} bytes)");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ESR] Callback length is 0 - no callback URL");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ESR] No callback data - end of data");
            }

            // Read info pairs (optional)
            System.Diagnostics.Debug.WriteLine($"[ESR] Reading info at offset {offset}, remaining: {data.Length - offset} bytes");
            if (offset < data.Length)
            {
                var infoCount = ReadVarUint(data, ref offset);
                System.Diagnostics.Debug.WriteLine($"[ESR] Info pair count: {infoCount}");
                
                if (infoCount > 0)
                {
                    request.Info = new Dictionary<string, object>();
                    for (var i = 0; i < infoCount && offset < data.Length; i++)
                    {
                        var keyLength = ReadVarUint(data, ref offset);
                        if (offset + (int)keyLength > data.Length)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ESR] Warning: Key length {keyLength} exceeds remaining data");
                            break;
                        }
                        var key = Encoding.UTF8.GetString(data, offset, (int)keyLength);
                        offset += (int)keyLength;

                        var valueLength = ReadVarUint(data, ref offset);
                        if (offset + (int)valueLength > data.Length)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ESR] Warning: Value length {valueLength} exceeds remaining data");
                            break;
                        }
                        var value = Encoding.UTF8.GetString(data, offset, (int)valueLength);
                        offset += (int)valueLength;

                        request.Info[key] = value;
                        System.Diagnostics.Debug.WriteLine($"[ESR] Info[{key}] = {(value.Length > 100 ? value[..100] + "..." : value)}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ESR] No info data - end of data");
            }
            
            System.Diagnostics.Debug.WriteLine($"[ESR] Parsing complete. Final offset: {offset}/{data.Length}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Result - Callback: {request.Callback ?? "(null)"}, Info count: {request.Info?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[ESR] Successfully parsed ESR request");
            return request;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ESR] Parse error at offset {offset}: {ex.Message}"
            );
            throw;
        }
    }

    private static object ParseAction(byte[] data, ref int offset)
    {
        // Read account name (8 bytes, name encoded)
        if (offset + 8 > data.Length)
            throw new InvalidOperationException(
                $"Not enough data for account name at offset {offset}"
            );
        var accountBytes = data.AsSpan(offset, 8).ToArray();
        var account = DecodeName(accountBytes);
        offset += 8;
        System.Diagnostics.Debug.WriteLine($"[ESR] Action account: {account}");

        // Read action name (8 bytes, name encoded)
        if (offset + 8 > data.Length)
            throw new InvalidOperationException(
                $"Not enough data for action name at offset {offset}"
            );
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
                throw new InvalidOperationException(
                    $"Not enough data for authorization at offset {offset}"
                );

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
            throw new InvalidOperationException(
                $"Not enough data for action data: need {dataLength} bytes, have {data.Length - offset}"
            );
        var actionData = Convert
            .ToHexString(data.AsSpan(offset, (int)dataLength).ToArray())
            .ToLowerInvariant();
        offset += (int)dataLength;
        System.Diagnostics.Debug.WriteLine($"[ESR] Action data length: {dataLength}");

        return new
        {
            account,
            name,
            authorization = authorizations,
            data = actionData,
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
            transaction_extensions = extensions,
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

    private static byte[] EncodeNameToBytes(string name)
    {
        const string charmap = ".12345abcdefghijklmnopqrstuvwxyz";
        
        if (string.IsNullOrEmpty(name))
            return new byte[8];

        ulong value = 0;
        var n = Math.Min(name.Length, 13);
        
        for (var i = 0; i < n; i++)
        {
            var c = charmap.IndexOf(name[i]);
            if (c == -1)
                c = 0; // Unknown char becomes '.'
            
            if (i < 12)
            {
                // First 12 characters use 5 bits each
                value |= (ulong)(uint)c << (64 - 5 * (i + 1));
            }
            else
            {
                // 13th character uses only 4 bits
                value |= (ulong)(uint)(c & 0x0F);
            }
        }
        
        return BitConverter.GetBytes(value);
    }

    private static string GetChainIdFromAlias(byte alias)
    {
        // Common chain aliases from ESR spec
        // See: https://github.com/greymass/eosio-signing-request/blob/master/src/chain-id.ts
        return alias switch
        {
            0 => throw new InvalidOperationException("Chain alias 0 means multi-chain request"),
            1 => "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", // EOS Mainnet
            2 => "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", // Telos
            3 => "e70aaab8997e1dfce58fbfac80cbbb8fecec7b99cf982a9444273cbc64c41473", // Jungle Testnet
            4 => "5fff1dae8dc8e2fc4d5b23b2c7665c97f9e9d8edf2b6485a86ba311c25639191", // Kylin Testnet
            5 => "73647cde120091e0a4b85bced2f3cfdb3041e266cbbe95cee59b73235a1b3b6f", // Worbli
            6 => "d5a3d18fbb3c084e3b1f3fa98c21014b5f3db536cc15d08f9f6479517c6a3d86", // BOS
            7 => "cfe6486a83bad4962f232d48003b1824ab5665c36778141034d75e57b956e422", // MeetOne
            8 => "b042025541e25a472bffde2d62edd457b7e70cee943412b1ea0f044f88591664", // Insights
            9 => "b912d19a6abd2b1b05611ae5be473355d64d95aeff0c09bedc8c166cd6468fe4", // BEOS
            10 => "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", // WAX Mainnet
            11 => "384da888112027f0321850a169f737c33e53b388aad48b5adace4bab97f437e0", // Proton
            12 => "21dcae42c0182200e93f954a074011f9048a7624c6fe81d3c9541a614a88bd1c", // FIO
            _ => throw new InvalidOperationException($"Unknown chain alias: {alias}"),
        };
    }

    private byte[] EncodeEsrData()
    {
        var json = JsonSerializer.Serialize(this);
        return Encoding.UTF8.GetBytes(json);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string encoded)
    {
        var base64 = encoded.Replace('-', '+').Replace('_', '/');

        // Add padding
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }
}
