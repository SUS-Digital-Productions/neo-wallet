using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.Sharp.Cryptography;

namespace SUS.EOS.EosioSigningRequest.Services;

/// <summary>
/// ESR Session Manager implementation with background thread WebSocket listener
/// Anchor Link compatible - no UI dependencies
/// </summary>
public class EsrSessionManager : IEsrSessionManager, IDisposable
{
    private const string DefaultLinkUrl = "cb.anchor.link";
    private readonly IEsrStateStore _stateStore;
    private readonly IEsrService _esrService;
    private readonly List<EsrSession> _sessions = new();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private Task? _listenerTask;
    private Task? _heartbeatTask;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private string? _linkId;
    private string? _requestKey;
    private string? _requestPublicKey;
    private bool _disposed;

    public event EventHandler<EsrSigningRequestEventArgs>? SigningRequestReceived;
    public event EventHandler<EsrSessionStatusEventArgs>? StatusChanged;

    public EsrSessionStatus Status { get; private set; } = EsrSessionStatus.Disconnected;
    public string LinkId => _linkId ?? string.Empty;
    public string? RequestPublicKey => _requestPublicKey;
    public IReadOnlyList<EsrSession> Sessions => _sessions.AsReadOnly();

    /// <summary>
    /// Create ESR Session Manager with custom state store
    /// </summary>
    public EsrSessionManager(IEsrStateStore stateStore, IEsrService esrService)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(esrService);
        
        _stateStore = stateStore;
        _esrService = esrService;
        LoadState();
    }

    /// <summary>
    /// Create ESR Session Manager with in-memory state (non-persistent)
    /// </summary>
    public EsrSessionManager(IEsrService esrService)
        : this(new MemoryEsrStateStore(), esrService) { }

    private void LoadState()
    {
        try
        {
            _linkId = _stateStore.Get("esr_link_id", string.Empty);
            _requestKey = _stateStore.Get("esr_request_key", string.Empty);

            // Generate new link ID and key if not exists
            if (string.IsNullOrEmpty(_linkId))
            {
                _linkId = GenerateLinkId();
                _stateStore.Set("esr_link_id", _linkId);
            }

            if (string.IsNullOrEmpty(_requestKey))
            {
                _requestKey = GenerateRequestKey();
                _stateStore.Set("esr_request_key", _requestKey);
                _requestPublicKey = DerivePublicKey(_requestKey);
            }
            else
            {
                // Try to derive public key - if it fails (old invalid format), regenerate
                try
                {
                    _requestPublicKey = DerivePublicKey(_requestKey);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Invalid stored key, regenerating: {ex.Message}");
                    _requestKey = GenerateRequestKey();
                    _stateStore.Set("esr_request_key", _requestKey);
                    _requestPublicKey = DerivePublicKey(_requestKey);
                }
            }

            // Load sessions
            var sessionsJson = _stateStore.Get("esr_sessions", string.Empty);
            if (!string.IsNullOrEmpty(sessionsJson))
            {
                try
                {
                    var sessions = JsonSerializer.Deserialize<List<EsrSession>>(sessionsJson);
                    if (sessions != null)
                    {
                        _sessions.AddRange(sessions);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Failed to load sessions: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to load state: {ex.Message}");
        }
    }

    private void SaveState()
    {
        try
        {
            _stateStore.Set("esr_link_id", _linkId ?? string.Empty);
            _stateStore.Set("esr_request_key", _requestKey ?? string.Empty);
            _stateStore.Set("esr_sessions", JsonSerializer.Serialize(_sessions));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to save state: {ex.Message}");
        }
    }

    private static string GenerateLinkId()
    {
        // Generate UUID v4 style link ID
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateRequestKey()
    {
        // Generate a secp256k1 private key (32 random bytes)
        using var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[32];
        rng.GetBytes(keyBytes);
        
        // Use EosioKey to get proper WIF format
        var key = EosioKey.FromBytes(keyBytes);
        return key.PrivateKeyWif;
    }

    private static string DerivePublicKey(string privateKeyWif)
    {
        // Use EosioKey to derive proper EOS public key in PUB_K1_ format (with RIPEMD160 checksum)
        // Anchor Link requires the modern format for session keys
        var key = EosioKey.FromPrivateKey(privateKeyWif);
        return key.PublicKeyK1;
    }

    /// <summary>
    /// Connect to Anchor Link relay with background thread listener
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (Status == EsrSessionStatus.Connected || Status == EsrSessionStatus.Connecting)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[ESR] Already connected or connecting. Status: {Status}"
                );
                return;
            }

            if (string.IsNullOrEmpty(_linkId))
            {
                throw new InvalidOperationException("Link ID not initialized");
            }

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Initiating connection to wss://{DefaultLinkUrl}/{_linkId}"
            );

            UpdateStatus(EsrSessionStatus.Connecting);
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            
            var uri = new Uri($"wss://{DefaultLinkUrl}/{_linkId}");

            System.Diagnostics.Trace.WriteLine($"[ESR] Connecting to WebSocket...");
            await _webSocket.ConnectAsync(uri, _connectionCts.Token);

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] WebSocket connected! State: {_webSocket.State}"
            );
            UpdateStatus(EsrSessionStatus.Connected);

            // Send identify message to register this wallet with the relay
            await SendIdentifyMessageAsync(_connectionCts.Token);

            // Start background listener thread
            System.Diagnostics.Trace.WriteLine("[ESR] Starting background listener thread...");
            _listenerTask = Task.Run(
                () => ListenForMessagesAsync(_connectionCts.Token),
                _connectionCts.Token
            );
            
            // Start heartbeat to keep connection alive
            System.Diagnostics.Trace.WriteLine("[ESR] Starting heartbeat thread...");
            _heartbeatTask = Task.Run(
                () => HeartbeatAsync(_connectionCts.Token),
                _connectionCts.Token
            );
        }
        catch (Exception ex)
        {
            UpdateStatus(EsrSessionStatus.Disconnected);
            System.Diagnostics.Trace.WriteLine($"[ESR] Connection failed: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Exception type: {ex.GetType().Name}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnect from Anchor Link relay and stop background listener
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Disconnecting...");
            
            _connectionCts?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disconnecting",
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Close error: {ex.Message}");
                }
            }

            // Wait for listener task to complete
            if (_listenerTask != null)
            {
                try
                {
                    await _listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Trace.WriteLine("[ESR] Listener task timeout");
                }
                _listenerTask = null;
            }
            
            if (_heartbeatTask != null)
            {
                try
                {
                    await _heartbeatTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Trace.WriteLine("[ESR] Heartbeat task timeout");
                }
                _heartbeatTask = null;
            }

            _webSocket?.Dispose();
            _webSocket = null;
            UpdateStatus(EsrSessionStatus.Disconnected);
            
            System.Diagnostics.Trace.WriteLine("[ESR] Disconnected");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Background thread that continuously listens for WebSocket messages
    /// </summary>
    private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.WriteLine("[ESR] Background listener thread started");
        var buffer = new byte[8192];
        var textMessageBuilder = new StringBuilder();
        var binaryMessageBuffer = new List<byte>();

        try
        {
            while (
                _webSocket?.State == WebSocketState.Open
                && !cancellationToken.IsCancellationRequested
            )
            {
                try
                {
                    System.Diagnostics.Trace.WriteLine("[ESR] Waiting for WebSocket message...");
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken
                    );

                    System.Diagnostics.Trace.WriteLine(
                        $"[ESR] Received message: Type={result.MessageType}, Count={result.Count}, EndOfMessage={result.EndOfMessage}"
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[ESR] WebSocket close received: {result.CloseStatus} - {result.CloseStatusDescription}"
                        );
                        UpdateStatus(EsrSessionStatus.Disconnected);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        textMessageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            var message = textMessageBuilder.ToString();
                            System.Diagnostics.Trace.WriteLine(
                                $"[ESR] Complete Text message received ({message.Length} chars): {message[..Math.Min(200, message.Length)]}..."
                            );
                            textMessageBuilder.Clear();
                            
                            // Process message asynchronously without blocking listener
                            _ = Task.Run(() => ProcessMessageAsync(message), cancellationToken);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Accumulate binary data
                        binaryMessageBuffer.AddRange(buffer.Take(result.Count));

                        if (result.EndOfMessage)
                        {
                            var binaryData = binaryMessageBuffer.ToArray();
                            System.Diagnostics.Trace.WriteLine(
                                $"[ESR] Complete Binary message received ({binaryData.Length} bytes)"
                            );
                            System.Diagnostics.Trace.WriteLine(
                                $"[ESR] First 100 bytes (hex): {Convert.ToHexString(binaryData[..Math.Min(100, binaryData.Length)])}"
                            );
                            
                            binaryMessageBuffer.Clear();
                            
                            // Binary WebSocket messages from Anchor Link are sealed messages
                            // Handle directly as encrypted payload
                            _ = Task.Run(() => ProcessBinaryMessageAsync(binaryData), cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when disconnecting
                    break;
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Connection closed prematurely");
                    UpdateStatus(EsrSessionStatus.Disconnected);
                    break;
                }
            }

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Listener thread exiting. State={_webSocket?.State}, Cancelled={cancellationToken.IsCancellationRequested}"
            );
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Listener thread cancelled");
        }
        catch (WebSocketException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] WebSocket error: {ex.Message}");
            UpdateStatus(EsrSessionStatus.Disconnected);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Listener thread error: {ex.Message}\n{ex.StackTrace}"
            );
            UpdateStatus(EsrSessionStatus.Disconnected);
        }
        finally
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Background listener thread stopped");
        }
    }

    /// <summary>
    /// Heartbeat to keep WebSocket connection alive
    /// </summary>
    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.WriteLine("[ESR] Heartbeat thread started");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        System.Diagnostics.Trace.WriteLine("[ESR] Sending heartbeat ping...");
                        var ping = JsonSerializer.Serialize(new { type = "ping" });
                        var bytes = Encoding.UTF8.GetBytes(ping);
                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken
                        );
                        System.Diagnostics.Trace.WriteLine($"[ESR] Heartbeat ping sent. Connection state: {_webSocket.State}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[ESR] Heartbeat failed: {ex.Message}");
                        UpdateStatus(EsrSessionStatus.Disconnected);
                        break;
                    }
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] WebSocket not open: {_webSocket?.State}");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Heartbeat cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Heartbeat error: {ex.Message}");
        }
        finally
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Heartbeat thread stopped");
        }
    }

    private async Task ProcessBinaryMessageAsync(byte[] binaryData)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] ========================================");
            System.Diagnostics.Trace.WriteLine($"[ESR] Processing binary sealed message ({binaryData.Length} bytes)");
            System.Diagnostics.Trace.WriteLine($"[ESR] ========================================");

            // Deserialize the SealedMessage binary format (Anchor Link protocol)
            // Format: public_key (34 bytes) + nonce (8 bytes) + ciphertext (variable) + checksum (4 bytes)
            var sealedMessage = DeserializeSealedMessage(binaryData);
            
            System.Diagnostics.Trace.WriteLine($"[ESR] SealedMessage deserialized:");
            System.Diagnostics.Trace.WriteLine($"[ESR]   From: {sealedMessage.From}");
            System.Diagnostics.Trace.WriteLine($"[ESR]   Nonce: {sealedMessage.Nonce}");
            System.Diagnostics.Trace.WriteLine($"[ESR]   Ciphertext: {sealedMessage.Ciphertext.Length} bytes");
            System.Diagnostics.Trace.WriteLine($"[ESR]   Checksum: {sealedMessage.Checksum}");

            // Decrypt the sealed message using our request key
            // The message is encrypted with ECDH using the sender's public key and our private request key
            System.Diagnostics.Trace.WriteLine($"[ESR] Decrypting with request key (our private key)...");
            var decrypted = DecryptSealedMessage(
                Convert.ToBase64String(sealedMessage.Ciphertext),  // ciphertext
                sealedMessage.From,                                 // senderPublicKey
                sealedMessage.Nonce.ToString(),                     // nonce
                _requestKey!                                        // receiverPrivateKey
            );

            System.Diagnostics.Trace.WriteLine($"[ESR] Decrypted message:");
            System.Diagnostics.Trace.WriteLine($"[ESR] {decrypted[..Math.Min(500, decrypted.Length)]}...");

            // The decrypted message should be an ESR (EOSIO Signing Request)
            // Parse it using the ESR service
            await ProcessDecryptedEsrAsync(decrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Binary message processing error: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Deserialize SealedMessage from binary format (Anchor Link protocol)
    /// Format: public_key (34 bytes) + nonce (8 bytes) + ciphertext (variable) + checksum (4 bytes)
    /// </summary>
    private (string From, ulong Nonce, byte[] Ciphertext, uint Checksum) DeserializeSealedMessage(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        // Read public key (34 bytes: 1 byte type + 33 bytes data)
        var keyType = reader.ReadByte();
        var keyData = reader.ReadBytes(33);
        
        string publicKey = keyType switch
        {
            0 => "PUB_K1_" + Base58CheckEncode(keyData, "K1"),
            1 => "PUB_R1_" + Base58CheckEncode(keyData, "R1"),
            _ => throw new InvalidOperationException($"Unknown public key type: {keyType}")
        };

        // Read nonce (8 bytes, little-endian uint64)
        var nonce = reader.ReadUInt64();

        // Read ciphertext length (variable uint32)
        var ciphertextLength = ReadVarUint32(reader);
        var ciphertext = reader.ReadBytes((int)ciphertextLength);

        // Read checksum (4 bytes, little-endian uint32)
        var checksum = reader.ReadUInt32();

        return (publicKey, nonce, ciphertext, checksum);
    }

    /// <summary>
    /// Process decrypted ESR string
    /// </summary>
    private async Task ProcessDecryptedEsrAsync(string esrString)
    {
        try
        {
            // Parse the ESR using the ESR service
            var esr = await _esrService.ParseRequestAsync(esrString);
            
            System.Diagnostics.Trace.WriteLine($"[ESR] ✅ ESR parsed successfully!");
            System.Diagnostics.Trace.WriteLine($"[ESR]   Chain ID: {esr.ChainId}");
            System.Diagnostics.Trace.WriteLine($"[ESR]   Callback: {esr.Callback}");
            
            // Fire the SigningRequestReceived event
            var eventArgs = new EsrSigningRequestEventArgs
            {
                Request = esr,
                RawPayload = esrString,
                IsIdentityRequest = false, // Binary WebSocket messages are not identity requests
                Callback = esr.Callback
            };
            
            SigningRequestReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to parse decrypted ESR: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Read variable-length uint32 (EOSIO format)
    /// </summary>
    private static uint ReadVarUint32(BinaryReader reader)
    {
        uint result = 0;
        byte shift = 0;
        
        while (true)
        {
            var b = reader.ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            
            if ((b & 0x80) == 0)
                break;
                
            shift += 7;
        }
        
        return result;
    }

    /// <summary>
    /// Base58Check encoding with suffix (for Antelope public keys)
    /// </summary>
    private static string Base58CheckEncode(byte[] data, string suffix)
    {
        // Calculate checksum: RIPEMD160(data + suffix) using BouncyCastle
        var ripemd = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var suffixBytes = Encoding.UTF8.GetBytes(suffix);
        
        var toHash = new byte[data.Length + suffixBytes.Length];
        Array.Copy(data, 0, toHash, 0, data.Length);
        Array.Copy(suffixBytes, 0, toHash, data.Length, suffixBytes.Length);
        
        var hash = new byte[ripemd.GetDigestSize()];
        ripemd.BlockUpdate(toHash, 0, toHash.Length);
        ripemd.DoFinal(hash, 0);

        // Append first 4 bytes of checksum to data
        var checksum = new byte[4];
        Array.Copy(hash, 0, checksum, 0, 4);
        var dataWithCheck = data.Concat(checksum).ToArray();
        
        // Base58 encode
        return Base58Encode(dataWithCheck);
    }

    /// <summary>
    /// Base58 encoding
    /// </summary>
    private static string Base58Encode(byte[] data)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var encoded = new System.Numerics.BigInteger(data.Reverse().Concat(new byte[] { 0 }).ToArray());
        var result = new StringBuilder();

        while (encoded > 0)
        {
            var remainder = (int)(encoded % 58);
            encoded /= 58;
            result.Insert(0, alphabet[remainder]);
        }

        // Add '1' for each leading zero byte
        foreach (var b in data)
        {
            if (b != 0) break;
            result.Insert(0, '1');
        }

        return result.ToString();
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] ========================================");
            System.Diagnostics.Trace.WriteLine($"[ESR] Processing message: {message}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Message length: {message.Length}");
            System.Diagnostics.Trace.WriteLine($"[ESR] ========================================");

            var envelope = JsonSerializer.Deserialize<EsrMessageEnvelope>(message);
            if (envelope == null)
            {
                System.Diagnostics.Trace.WriteLine("[ESR] Failed to deserialize envelope");
                return;
            }

            System.Diagnostics.Trace.WriteLine($"[ESR] Envelope type: {envelope.Type}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Has Payload: {!string.IsNullOrEmpty(envelope.Payload)}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Has Ciphertext: {!string.IsNullOrEmpty(envelope.Ciphertext)}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Has From: {!string.IsNullOrEmpty(envelope.From)}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Has Callback: {!string.IsNullOrEmpty(envelope.Callback)}");

            switch (envelope.Type)
            {
                case "sealed_message":
                    System.Diagnostics.Trace.WriteLine("[ESR] Handling sealed (encrypted) message...");
                    await HandleSealedMessageAsync(envelope);
                    break;
                    
                case "request":
                    System.Diagnostics.Trace.WriteLine("[ESR] Handling signing request...");
                    await HandleSigningRequestAsync(envelope);
                    break;

                case "identity":
                    System.Diagnostics.Trace.WriteLine("[ESR] Handling identity request...");
                    await HandleIdentityRequestAsync(envelope);
                    break;

                case "ping":
                    System.Diagnostics.Trace.WriteLine("[ESR] Handling ping...");
                    await SendPongAsync();
                    break;

                default:
                    System.Diagnostics.Trace.WriteLine(
                        $"[ESR] Unknown message type: {envelope.Type}"
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Message processing error: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Send identify message to register wallet with Anchor Link relay
    /// </summary>
    private async Task SendIdentifyMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Sending identify message...");

            // Anchor Link identify message format
            var identifyMessage = new
            {
                type = "identify",
                payload = new
                {
                    link_url = $"wss://{DefaultLinkUrl}/{_linkId}",
                    link_id = _linkId,
                    link_name = "NeoWallet",
                    link_key = _requestPublicKey,
                    device_id = _linkId,
                    device_key = _requestPublicKey,
                    chains = new[]
                    {
                        new
                        {
                            chain_id = "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", // WAX mainnet
                            chain_name = "WAX"
                        },
                        new
                        {
                            chain_id = "8fc6dce7942189f842170de953932b1f66693ad3788f766e777b6f9d22335c02", // WAX testnet
                            chain_name = "WAX Testnet"
                        },
                        new
                        {
                            chain_id = "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", // EOS mainnet
                            chain_name = "EOS"
                        },
                        new
                        {
                            chain_id = "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", // Telos mainnet
                            chain_name = "Telos"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(identifyMessage);
            var bytes = Encoding.UTF8.GetBytes(json);

            System.Diagnostics.Trace.WriteLine($"[ESR] Identify message: {json}");

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken
                );

                System.Diagnostics.Trace.WriteLine("[ESR] Identify message sent successfully");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[ESR] Cannot send identify - WebSocket state: {_webSocket?.State}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to send identify message: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Handle sealed (encrypted) messages from Anchor Link relay
    /// These messages are encrypted with ECDH shared secret
    /// </summary>
    private async Task HandleSealedMessageAsync(EsrMessageEnvelope envelope)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Sealed message from: {envelope.From}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Nonce: {envelope.Nonce}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Ciphertext length: {envelope.Ciphertext?.Length ?? 0}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Payload length: {envelope.Payload?.Length ?? 0}");

            if (string.IsNullOrEmpty(_requestKey))
            {
                System.Diagnostics.Trace.WriteLine("[ESR] No request key available for decryption");
                return;
            }

            if (string.IsNullOrEmpty(envelope.From))
            {
                System.Diagnostics.Trace.WriteLine("[ESR] No sender public key in sealed message");
                return;
            }

            // Get ciphertext from either field
            var ciphertext = envelope.Ciphertext ?? envelope.Payload;
            if (string.IsNullOrEmpty(ciphertext))
            {
                System.Diagnostics.Trace.WriteLine("[ESR] No ciphertext in sealed message");
                return;
            }

            // Decrypt the message
            string decryptedPayload;
            try
            {
                decryptedPayload = DecryptSealedMessage(
                    ciphertext,
                    envelope.From,
                    envelope.Nonce,
                    _requestKey
                );
                System.Diagnostics.Trace.WriteLine($"[ESR] Decrypted payload ({decryptedPayload.Length} chars): {decryptedPayload[..Math.Min(100, decryptedPayload.Length)]}...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ESR] Decryption failed: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
                return;
            }

            // Parse the decrypted message - it could be JSON or ESR URI
            if (decryptedPayload.StartsWith("{"))
            {
                // Try to parse as JSON envelope
                try
                {
                    var innerEnvelope = JsonSerializer.Deserialize<EsrMessageEnvelope>(decryptedPayload);
                    if (innerEnvelope != null)
                    {
                        innerEnvelope.From = envelope.From;
                        innerEnvelope.Callback = innerEnvelope.Callback ?? envelope.Callback;
                        await ProcessInnerMessageAsync(innerEnvelope);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ESR] Failed to parse as JSON: {ex.Message}");
                }
            }
            
            // Treat as ESR URI or direct payload
            if (decryptedPayload.StartsWith("esr:") || decryptedPayload.StartsWith("esr://"))
            {
                var innerEnvelope = new EsrMessageEnvelope
                {
                    Type = "request",
                    Payload = decryptedPayload,
                    Callback = envelope.Callback,
                    From = envelope.From
                };
                await HandleSigningRequestAsync(innerEnvelope);
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[ESR] Unknown decrypted payload format");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to handle sealed message: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Decrypt sealed message using ECDH + AES
    /// </summary>
    private static string DecryptSealedMessage(string ciphertext, string senderPublicKey, string? nonce, string receiverPrivateKey)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR] Decrypting with sender: {senderPublicKey}");
        
        // Decode ciphertext
        var ciphertextBytes = Convert.FromBase64String(ciphertext);
        System.Diagnostics.Trace.WriteLine($"[ESR] Ciphertext bytes: {ciphertextBytes.Length}");
        
        // Parse sender's public key
        var senderPubKeyBytes = EosioKey.ParsePublicKey(senderPublicKey);
        System.Diagnostics.Trace.WriteLine($"[ESR] Sender public key bytes: {senderPubKeyBytes.Length}");
        
        // Parse receiver's private key
        var receiverKey = EosioKey.FromPrivateKey(receiverPrivateKey);
        var receiverPrivKeyBytes = receiverKey.GetPrivateKeyBytes();
        System.Diagnostics.Trace.WriteLine($"[ESR] Receiver private key bytes: {receiverPrivKeyBytes.Length}");
        
        // Compute ECDH shared secret
        var rawSharedSecret = ComputeEcdhSharedSecret(receiverPrivKeyBytes, senderPubKeyBytes);
        System.Diagnostics.Trace.WriteLine($"[ESR] Raw shared secret computed: {rawSharedSecret.Length} bytes");

        // Wharfkit's PrivateKey.sharedSecret() actually returns SHA512(ecdh_point), not the raw point
        // So we need to hash it first
        var sharedSecret = SHA512.HashData(rawSharedSecret);
        System.Diagnostics.Trace.WriteLine($"[ESR] Hashed shared secret computed: {sharedSecret.Length} bytes");

        // Derive AES key and IV from nonce + hashed_shared_secret using SHA-512
        // Following Anchor Link protocol: SHA512(nonce_bytes + SHA512(ecdh_point))
        // Key = bytes 0-32, IV = bytes 32-48
        byte[] nonceBytes;
        if (!string.IsNullOrEmpty(nonce))
        {
            // Nonce is provided as uint64 string, convert to 8-byte little-endian
            var nonceValue = ulong.Parse(nonce);
            nonceBytes = BitConverter.GetBytes(nonceValue);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(nonceBytes);
            System.Diagnostics.Trace.WriteLine($"[ESR] Nonce: {nonceValue} -> {Convert.ToHexString(nonceBytes)}");
        }
        else
        {
            throw new InvalidOperationException("No nonce provided for decryption");
        }
        
        // Concatenate nonce + shared secret and hash with SHA-512
        var toHash = new byte[nonceBytes.Length + sharedSecret.Length];
        Array.Copy(nonceBytes, 0, toHash, 0, nonceBytes.Length);
        Array.Copy(sharedSecret, 0, toHash, nonceBytes.Length, sharedSecret.Length);
        
        using var sha512 = SHA512.Create();
        var derivedKey = sha512.ComputeHash(toHash);
        var aesKey = derivedKey[..32]; // First 32 bytes for AES-256 key
        var iv = derivedKey[32..48];    // Next 16 bytes for IV
        System.Diagnostics.Trace.WriteLine($"[ESR] AES key/IV derived: {aesKey.Length}/{iv.Length} bytes");
        
        // Decrypt using AES-CBC
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Decrypting {ciphertextBytes.Length} bytes of ciphertext...");
        System.Diagnostics.Trace.WriteLine($"[ESR] Ciphertext (first 64 bytes): {Convert.ToHexString(ciphertextBytes[..Math.Min(64, ciphertextBytes.Length)])}");
        
        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Decrypted {decryptedBytes.Length} bytes");
        System.Diagnostics.Trace.WriteLine($"[ESR] Decrypted (first 200 bytes): {Encoding.UTF8.GetString(decryptedBytes[..Math.Min(200, decryptedBytes.Length)])}");
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// Compute ECDH shared secret using secp256k1
    /// </summary>
    private static byte[] ComputeEcdhSharedSecret(byte[] privateKey, byte[] publicKey)
    {
        // Use BouncyCastle for ECDH on secp256k1 curve
        var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
        var domainParams = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(
            curve.Curve, curve.G, curve.N, curve.H);
        
        // Parse public key point (compressed 33-byte format)
        var pubKeyPoint = curve.Curve.DecodePoint(publicKey);
        
        // Create private key as BigInteger
        var privKeyInt = new Org.BouncyCastle.Math.BigInteger(1, privateKey);
        
        // Compute shared point: pubKey * privKey
        var sharedPoint = pubKeyPoint.Multiply(privKeyInt).Normalize();
        
        // Return X coordinate as shared secret (32 bytes)
        // GetEncoded() returns the field element in big-endian format
        var xCoord = sharedPoint.AffineXCoord.GetEncoded();
        
        System.Diagnostics.Trace.WriteLine($"[ESR] X coordinate length: {xCoord.Length}");
        // Security: Do not log the actual X coordinate value as it's the ECDH shared secret
        
        // Ensure it's exactly 32 bytes (pad with leading zeros if needed)
        if (xCoord.Length < 32)
        {
            var padded = new byte[32];
            Array.Copy(xCoord, 0, padded, 32 - xCoord.Length, xCoord.Length);
            System.Diagnostics.Trace.WriteLine($"[ESR] Padded to {padded.Length} bytes");
            return padded;
        }
        
        return xCoord;
    }

    /// <summary>
    /// Process inner decrypted message
    /// </summary>
    private async Task ProcessInnerMessageAsync(EsrMessageEnvelope envelope)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR] Processing inner message type: {envelope.Type}");
        
        switch (envelope.Type?.ToLowerInvariant())
        {
            case "request":
            case "signing_request":
                await HandleSigningRequestAsync(envelope);
                break;
            case "identity":
            case "identity_request":
                await HandleIdentityRequestAsync(envelope);
                break;
            default:
                System.Diagnostics.Trace.WriteLine($"[ESR] Unknown inner message type: {envelope.Type}");
                // Try to handle as signing request anyway
                if (!string.IsNullOrEmpty(envelope.Payload))
                {
                    await HandleSigningRequestAsync(envelope);
                }
                break;
        }
    }

    private async Task HandleSigningRequestAsync(EsrMessageEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Empty payload in signing request");
            return;
        }

        try
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Parsing ESR payload: {envelope.Payload[..Math.Min(50, envelope.Payload.Length)]}..."
            );

            // Decrypt payload if needed (using request key)
            var esrPayload = envelope.Payload;

            // Parse ESR
            var request = await _esrService.ParseRequestAsync(esrPayload);

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Parsed request: ChainId={request.ChainId}, Callback={request.Callback}"
            );

            // Find matching session
            var session = _sessions.FirstOrDefault(s => s.ChainId == request.ChainId);

            if (session != null)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[ESR] Found matching session: {session.Actor}@{session.Permission}"
                );
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[ESR] No matching session found for chain {request.ChainId}"
                );
            }

            // Raise event for UI to handle
            var args = new EsrSigningRequestEventArgs
            {
                Request = request,
                RawPayload = esrPayload,
                Session = session,
                Callback = envelope.Callback,
            };

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Raising SigningRequestReceived event. Has subscribers: {SigningRequestReceived != null}"
            );
            SigningRequestReceived?.Invoke(this, args);
            System.Diagnostics.Trace.WriteLine("[ESR] Event raised successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Failed to handle signing request: {ex.Message}"
            );
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task HandleIdentityRequestAsync(EsrMessageEnvelope envelope)
    {
        // Identity requests are handled similarly but create/update sessions
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Empty payload in identity request");
            return;
        }

        try
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Parsing identity ESR: {envelope.Payload[..Math.Min(50, envelope.Payload.Length)]}..."
            );

            var request = await _esrService.ParseRequestAsync(envelope.Payload);

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Parsed identity request: ChainId={request.ChainId}"
            );

            var args = new EsrSigningRequestEventArgs
            {
                Request = request,
                RawPayload = envelope.Payload,
                IsIdentityRequest = true,
                Callback = envelope.Callback,
            };

            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Raising SigningRequestReceived event for identity. Has subscribers: {SigningRequestReceived != null}"
            );
            SigningRequestReceived?.Invoke(this, args);
            System.Diagnostics.Trace.WriteLine("[ESR] Identity event raised successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ESR] Failed to handle identity request: {ex.Message}"
            );
            System.Diagnostics.Trace.WriteLine($"[ESR] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task SendPongAsync()
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        try
        {
            var pong = JsonSerializer.Serialize(new { type = "pong" });
            var bytes = Encoding.UTF8.GetBytes(pong);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
            System.Diagnostics.Trace.WriteLine("[ESR] Pong sent");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to send pong: {ex.Message}");
        }
    }

    public async Task SendCallbackAsync(EsrCallbackPayload callback)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            System.Diagnostics.Trace.WriteLine("[ESR] Cannot send callback - WebSocket not open");
            return;
        }

        try
        {
            var message = new { type = "callback", payload = callback };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
            System.Diagnostics.Trace.WriteLine($"[ESR] Callback sent");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ESR] Failed to send callback: {ex.Message}");
        }
    }

    public async Task AddSessionAsync(EsrSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        
        // Remove existing session for same actor/permission/chain
        _sessions.RemoveAll(s =>
            s.Actor == session.Actor
            && s.Permission == session.Permission
            && s.ChainId == session.ChainId
        );

        session.Created = DateTime.UtcNow;
        session.LastUsed = DateTime.UtcNow;
        _sessions.Add(session);
        SaveState();
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Session added: {session.Actor}@{session.Permission} on {session.ChainId}");
        await Task.CompletedTask;
    }

    public async Task RemoveSessionAsync(EsrSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        
        var removed = _sessions.RemoveAll(s =>
            s.Actor == session.Actor
            && s.Permission == session.Permission
            && s.ChainId == session.ChainId
        );
        
        if (removed > 0)
        {
            SaveState();
            System.Diagnostics.Trace.WriteLine($"[ESR] Session removed: {session.Actor}@{session.Permission}");
        }
        
        await Task.CompletedTask;
    }

    public async Task ClearSessionsAsync()
    {
        var count = _sessions.Count;
        _sessions.Clear();
        SaveState();
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Cleared {count} sessions");
        await Task.CompletedTask;
    }

    private void UpdateStatus(EsrSessionStatus status)
    {
        if (Status != status)
        {
            var oldStatus = Status;
            Status = status;
            System.Diagnostics.Trace.WriteLine($"[ESR] Status changed: {oldStatus} -> {status}");
            StatusChanged?.Invoke(this, new EsrSessionStatusEventArgs { Status = status });
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
            
        System.Diagnostics.Trace.WriteLine("[ESR] Disposing...");
        _disposed = true;

        _connectionCts?.Cancel();
        
        // Wait for listener task with timeout
        if (_listenerTask != null)
        {
            try
            {
                _listenerTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Task already completed or cancelled
            }
        }
        
        _connectionCts?.Dispose();
        _webSocket?.Dispose();
        _connectionLock?.Dispose();
        
        System.Diagnostics.Trace.WriteLine("[ESR] Disposed");
    }
}
