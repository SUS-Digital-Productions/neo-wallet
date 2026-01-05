using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SUS.EOS.Sharp.Services;

/// <summary>
/// Light API client for querying blockchain data across multiple EOSIO chains
/// Light API provides fast access to account, key, and balance data
/// </summary>
public class LightApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Known Light API endpoints for various chains
    /// </summary>
    public static readonly Dictionary<string, string> KnownEndpoints = new()
    {
        // WAX
        { "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4", "https://wax.light-api.net" },
        // EOS
        { "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906", "https://eos.light-api.net" },
        // Telos
        { "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11", "https://telos.light-api.net" },
        // Proton
        { "384da888112027f0321850a169f737c33e53b388aad48b5adace4bab97f437e0", "https://proton.light-api.net" },
        // Libre
        { "38b1d7815474d0c60683ecbea321d723e83f5da6ae5f1c1f9fecc69d9ba96465", "https://libre.light-api.net" },
        // UX Network
        { "8fc6dce7942189f842170de953932b1f66693ad3788f766e777b6f9d22335c02", "https://ux.light-api.net" },
        // FIO
        { "21dcae42c0182200e93f954a074011f9048a7624c6fe81d3c9541a614a88bd1c", "https://fio.light-api.net" },
    };

    /// <summary>
    /// Create a new Light API client
    /// </summary>
    /// <param name="baseEndpoint">Base endpoint URL (e.g., "https://wax.light-api.net")</param>
    public LightApiClient(string baseEndpoint)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseEndpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    /// <summary>
    /// Create a Light API client for a known chain by chain ID
    /// </summary>
    public static LightApiClient? ForChain(string chainId)
    {
        if (KnownEndpoints.TryGetValue(chainId, out var endpoint))
        {
            return new LightApiClient(endpoint);
        }
        return null;
    }

    /// <summary>
    /// Normalize a public key to PUB_K1_ format for Light API compatibility.
    /// Converts legacy EOS... keys to PUB_K1_ format.
    /// </summary>
    private static string NormalizePublicKey(string publicKey)
    {
        if (string.IsNullOrEmpty(publicKey))
            return publicKey;

        // Already in new format
        if (publicKey.StartsWith("PUB_K1_") || publicKey.StartsWith("PUB_R1_"))
            return publicKey;

        // Convert legacy EOS... format to PUB_K1_
        if (publicKey.StartsWith("EOS"))
        {
            try
            {
                // Decode from legacy format (Base58 with RIPEMD160 checksum)
                var decoded = Base58Decode(publicKey[3..]);
                if (decoded.Length < 4) return publicKey;
                
                // Remove 4-byte checksum (RIPEMD160 of key)
                var keyData = decoded[..^4];
                
                // Re-encode with K1 suffix checksum using RIPEMD160
                return "PUB_K1_" + Base58CheckEncode(keyData, "K1");
            }
            catch
            {
                // If conversion fails, return original
                return publicKey;
            }
        }

        return publicKey;
    }

    #region Base58 Helpers

    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static byte[] Base58Decode(string encoded)
    {
        var value = Org.BouncyCastle.Math.BigInteger.Zero;

        foreach (var c in encoded)
        {
            var digit = Base58Alphabet.IndexOf(c);
            if (digit < 0)
                throw new FormatException($"Invalid Base58 character: {c}");
            value = value.Multiply(Org.BouncyCastle.Math.BigInteger.ValueOf(58))
                        .Add(Org.BouncyCastle.Math.BigInteger.ValueOf(digit));
        }

        var bytes = value.ToByteArray();
        
        // Remove leading zero byte if present (BigInteger sign byte)
        if (bytes.Length > 1 && bytes[0] == 0)
        {
            var trimmed = new byte[bytes.Length - 1];
            Array.Copy(bytes, 1, trimmed, 0, trimmed.Length);
            bytes = trimmed;
        }

        // Add leading zeros
        var leadingZeros = encoded.TakeWhile(c => c == '1').Count();
        if (leadingZeros > 0)
        {
            var withLeadingZeros = new byte[bytes.Length + leadingZeros];
            Array.Copy(bytes, 0, withLeadingZeros, leadingZeros, bytes.Length);
            bytes = withLeadingZeros;
        }

        return bytes;
    }

    private static string Base58Encode(byte[] data)
    {
        var value = new Org.BouncyCastle.Math.BigInteger(1, data);
        var result = new List<char>();

        while (value.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) > 0)
        {
            var remainder = value.Mod(Org.BouncyCastle.Math.BigInteger.ValueOf(58));
            value = value.Divide(Org.BouncyCastle.Math.BigInteger.ValueOf(58));
            result.Insert(0, Base58Alphabet[remainder.IntValue]);
        }

        // Add leading zeros
        foreach (var b in data)
        {
            if (b != 0) break;
            result.Insert(0, '1');
        }

        return new string(result.ToArray());
    }

    private static string Base58CheckEncode(byte[] data, string suffix)
    {
        var ripemd = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var suffixBytes = Encoding.UTF8.GetBytes(suffix);
        
        var toHash = new byte[data.Length + suffixBytes.Length];
        Array.Copy(data, 0, toHash, 0, data.Length);
        Array.Copy(suffixBytes, 0, toHash, data.Length, suffixBytes.Length);
        
        var hash = new byte[ripemd.GetDigestSize()];
        ripemd.BlockUpdate(toHash, 0, toHash.Length);
        ripemd.DoFinal(hash, 0);
        
        var withChecksum = new byte[data.Length + 4];
        Array.Copy(data, 0, withChecksum, 0, data.Length);
        Array.Copy(hash, 0, withChecksum, data.Length, 4);
        
        return Base58Encode(withChecksum);
    }

    #endregion

    /// <summary>
    /// Get all accounts associated with a public key across all chains tracked by this Light API endpoint
    /// </summary>
    /// <param name="publicKey">Public key in PUB_K1_, PUB_R1_, or legacy EOS format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of chain name to list of account permissions</returns>
    public async Task<LightApiKeyResponse> GetAccountsByKeyAsync(string publicKey, CancellationToken cancellationToken = default)
    {
        // Convert legacy EOS... format to PUB_K1_ format for Light API
        var normalizedKey = NormalizePublicKey(publicKey);
        
        var response = await _httpClient.GetAsync($"/api/key/{normalizedKey}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Parse the response - it's a dynamic structure with chain names as keys
        var result = new LightApiKeyResponse();
        
        using var doc = JsonDocument.Parse(json);
        foreach (var chainProperty in doc.RootElement.EnumerateObject())
        {
            var chainName = chainProperty.Name;
            var chainData = new LightApiChainData { ChainName = chainName };

            if (chainProperty.Value.TryGetProperty("chain", out var chainInfo))
            {
                chainData.ChainId = chainInfo.TryGetProperty("chainid", out var cid) ? cid.GetString() ?? "" : "";
                chainData.SystemToken = chainInfo.TryGetProperty("systoken", out var st) ? st.GetString() ?? "" : "";
                chainData.Decimals = chainInfo.TryGetProperty("decimals", out var dec) ? dec.GetInt32() : 4;
            }

            if (chainProperty.Value.TryGetProperty("accounts", out var accounts))
            {
                foreach (var accountProperty in accounts.EnumerateObject())
                {
                    var accountName = accountProperty.Name;
                    
                    foreach (var permElement in accountProperty.Value.EnumerateArray())
                    {
                        var permission = new LightApiAccountPermission
                        {
                            AccountName = accountName,
                            Permission = permElement.TryGetProperty("perm", out var perm) ? perm.GetString() ?? "active" : "active",
                            Threshold = permElement.TryGetProperty("threshold", out var th) ? th.GetInt32() : 1
                        };

                        // Check if this permission is controlled by a key (not by another account)
                        if (permElement.TryGetProperty("auth", out var auth))
                        {
                            if (auth.TryGetProperty("keys", out var keys))
                            {
                                foreach (var key in keys.EnumerateArray())
                                {
                                    var pubKey = key.TryGetProperty("public_key", out var pk) ? pk.GetString() :
                                                 key.TryGetProperty("pubkey", out var pk2) ? pk2.GetString() : null;
                                    
                                    if (!string.IsNullOrEmpty(pubKey))
                                    {
                                        permission.PublicKeys.Add(pubKey);
                                        permission.IsKeyControlled = true;
                                    }
                                }
                            }
                        }

                        chainData.Accounts.Add(permission);
                    }
                }
            }

            result.Chains[chainName] = chainData;
        }

        return result;
    }

    /// <summary>
    /// Get account balances
    /// </summary>
    public async Task<List<LightApiBalance>> GetAccountBalancesAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/balances/{accountName}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = new List<LightApiBalance>();

        using var doc = JsonDocument.Parse(json);
        
        // Find the balances array in the response
        foreach (var chainProperty in doc.RootElement.EnumerateObject())
        {
            if (chainProperty.Value.TryGetProperty("balances", out var balances))
            {
                foreach (var balance in balances.EnumerateArray())
                {
                    result.Add(new LightApiBalance
                    {
                        Contract = balance.TryGetProperty("contract", out var c) ? c.GetString() ?? "" : "",
                        Amount = balance.TryGetProperty("amount", out var a) ? a.GetString() ?? "0" : "0",
                        Currency = balance.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "" : "",
                        Decimals = balance.TryGetProperty("decimals", out var d) ? d.GetInt32() : 4
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get account information
    /// </summary>
    public async Task<LightApiAccountInfo?> GetAccountInfoAsync(string accountName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/account/{accountName}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var doc = JsonDocument.Parse(json);
            
            // Parse first chain's account info
            foreach (var chainProperty in doc.RootElement.EnumerateObject())
            {
                if (chainProperty.Value.TryGetProperty("account_name", out var name))
                {
                    return new LightApiAccountInfo
                    {
                        AccountName = name.GetString() ?? accountName,
                        Created = chainProperty.Value.TryGetProperty("created", out var cr) ? cr.GetString() ?? "" : "",
                        RamQuota = chainProperty.Value.TryGetProperty("ram_quota", out var rq) ? rq.GetInt64() : 0,
                        RamUsage = chainProperty.Value.TryGetProperty("ram_usage", out var ru) ? ru.GetInt64() : 0,
                        CpuWeight = chainProperty.Value.TryGetProperty("cpu_weight", out var cw) ? cw.GetInt64() : 0,
                        NetWeight = chainProperty.Value.TryGetProperty("net_weight", out var nw) ? nw.GetInt64() : 0
                    };
                }
            }
        }
        catch
        {
            // Account may not exist
        }

        return null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

#region Response Models

/// <summary>
/// Response from Light API key lookup
/// </summary>
public class LightApiKeyResponse
{
    public Dictionary<string, LightApiChainData> Chains { get; set; } = new();
}

/// <summary>
/// Chain-specific data from Light API
/// </summary>
public class LightApiChainData
{
    public string ChainName { get; set; } = "";
    public string ChainId { get; set; } = "";
    public string SystemToken { get; set; } = "";
    public int Decimals { get; set; } = 4;
    public List<LightApiAccountPermission> Accounts { get; set; } = new();
}

/// <summary>
/// Account permission from Light API
/// </summary>
public class LightApiAccountPermission
{
    public string AccountName { get; set; } = "";
    public string Permission { get; set; } = "active";
    public int Threshold { get; set; } = 1;
    public bool IsKeyControlled { get; set; }
    public List<string> PublicKeys { get; set; } = new();
}

/// <summary>
/// Balance information from Light API
/// </summary>
public class LightApiBalance
{
    public string Contract { get; set; } = "";
    public string Amount { get; set; } = "0";
    public string Currency { get; set; } = "";
    public int Decimals { get; set; } = 4;
}

/// <summary>
/// Account information from Light API
/// </summary>
public class LightApiAccountInfo
{
    public string AccountName { get; set; } = "";
    public string Created { get; set; } = "";
    public long RamQuota { get; set; }
    public long RamUsage { get; set; }
    public long CpuWeight { get; set; }
    public long NetWeight { get; set; }
}

#endregion
