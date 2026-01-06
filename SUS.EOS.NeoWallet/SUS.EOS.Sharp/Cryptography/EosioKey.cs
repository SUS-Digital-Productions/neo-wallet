using System.Security.Cryptography;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math.EC;
using Cryptography.ECDSA;

namespace SUS.EOS.Sharp.Cryptography;

/// <summary>
/// Supported private key formats
/// </summary>
public enum KeyFormat
{
    /// <summary>Unknown format</summary>
    Unknown,
    /// <summary>Legacy WIF format (starts with 5)</summary>
    LegacyWif,
    /// <summary>Modern PVT_K1_ format</summary>
    ModernK1,
    /// <summary>Modern PVT_R1_ format (secp256r1)</summary>
    ModernR1,
    /// <summary>Raw 32-byte hex string (64 characters)</summary>
    RawHex,
    /// <summary>Raw 32-byte array</summary>
    RawBytes
}

/// <summary>
/// EOSIO key management with support for multiple key formats:
/// - WIF (Wallet Import Format) - legacy format starting with '5'
/// - Modern format (PVT_K1_xxx or PVT_R1_xxx)  
/// - Raw hex (64 character hex string)
/// - Raw bytes (32 byte array)
/// </summary>
public sealed class EosioKey
{
    private readonly byte[] _privateKeyBytes;
    private readonly byte[] _publicKeyBytes;
    private readonly KeyFormat _format;

    public string PrivateKeyWif { get; }
    public string PublicKey { get; }
    
    /// <summary>
    /// The original format this key was imported from
    /// </summary>
    public KeyFormat OriginalFormat => _format;
    
    /// <summary>
    /// Gets the raw 32-byte private key
    /// </summary>
    public byte[] GetPrivateKeyBytes() => (byte[])_privateKeyBytes.Clone();
    
    /// <summary>
    /// Gets the compressed public key bytes (33 bytes)
    /// </summary>
    public byte[] GetPublicKeyBytes() => (byte[])_publicKeyBytes.Clone();
    
    /// <summary>
    /// Gets the private key as a hex string
    /// </summary>
    public string PrivateKeyHex => Convert.ToHexString(_privateKeyBytes).ToLowerInvariant();
    
    /// <summary>
    /// Gets the public key in modern PUB_K1_ format
    /// </summary>
    public string PublicKeyK1 => EncodePublicKeyK1(_publicKeyBytes);

    private EosioKey(byte[] privateKeyBytes, byte[] publicKeyBytes, string privateKeyWif, string publicKey, KeyFormat format)
    {
        _privateKeyBytes = privateKeyBytes;
        _publicKeyBytes = publicKeyBytes;
        PrivateKeyWif = privateKeyWif;
        PublicKey = publicKey;
        _format = format;
    }

    /// <summary>
    /// Creates key from any supported format (WIF, PVT_K1_, PVT_R1_, hex, bytes)
    /// </summary>
    public static EosioKey FromPrivateKey(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
            throw new ArgumentException("Private key cannot be empty", nameof(privateKey));

        var trimmed = privateKey.Trim();
        var format = DetectFormat(trimmed);

        return format switch
        {
            KeyFormat.LegacyWif => FromWif(trimmed),
            KeyFormat.ModernK1 => FromModernK1(trimmed),
            KeyFormat.ModernR1 => throw new NotSupportedException("R1 (secp256r1) keys are not yet supported"),
            KeyFormat.RawHex => FromHex(trimmed),
            _ => throw new ArgumentException($"Unknown private key format: {privateKey[..Math.Min(10, privateKey.Length)]}...", nameof(privateKey))
        };
    }

    /// <summary>
    /// Creates key from raw 32-byte array
    /// </summary>
    public static EosioKey FromBytes(byte[] privateKeyBytes)
    {
        if (privateKeyBytes == null || privateKeyBytes.Length != 32)
            throw new ArgumentException("Private key must be exactly 32 bytes", nameof(privateKeyBytes));

        var publicKeyBytes = DerivePublicKey(privateKeyBytes);
        var publicKey = EncodePublicKey(publicKeyBytes);
        var wif = EncodeWif(privateKeyBytes);

        return new EosioKey(privateKeyBytes, publicKeyBytes, wif, publicKey, KeyFormat.RawBytes);
    }

    /// <summary>
    /// Creates key from raw hex string (64 characters)
    /// </summary>
    public static EosioKey FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex string cannot be empty", nameof(hex));

        var trimmed = hex.Trim();
        
        // Remove optional 0x prefix
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        if (trimmed.Length != 64)
            throw new ArgumentException($"Hex private key must be 64 characters (got {trimmed.Length})", nameof(hex));

        if (!IsValidHex(trimmed))
            throw new ArgumentException("Invalid hex characters in private key", nameof(hex));

        var privateKeyBytes = Convert.FromHexString(trimmed);
        var publicKeyBytes = DerivePublicKey(privateKeyBytes);
        var publicKey = EncodePublicKey(publicKeyBytes);
        var wif = EncodeWif(privateKeyBytes);

        return new EosioKey(privateKeyBytes, publicKeyBytes, wif, publicKey, KeyFormat.RawHex);
    }

    /// <summary>
    /// Creates key from WIF (Wallet Import Format) private key
    /// </summary>
    public static EosioKey FromWif(string privateKeyWif)
    {
        if (string.IsNullOrWhiteSpace(privateKeyWif))
            throw new ArgumentException("Private key cannot be empty", nameof(privateKeyWif));

        byte[] privateKeyBytes;
        KeyFormat format;
        
        // Check if it's a legacy or modern format
        if (privateKeyWif.StartsWith("PVT_K1_"))
        {
            format = KeyFormat.ModernK1;
            var data = privateKeyWif[7..];
            privateKeyBytes = Base58CheckDecode(data, "K1");
        }
        else if (privateKeyWif.StartsWith("PVT_R1_"))
        {
            throw new NotSupportedException("R1 (secp256r1) keys are not yet supported");
        }
        else
        {
            // Legacy WIF format (starts with 5)
            format = KeyFormat.LegacyWif;
            privateKeyBytes = Base58CheckDecode(privateKeyWif, new byte[] { 0x80 });
        }

        // Derive public key from private key using secp256k1
        var publicKeyBytes = DerivePublicKey(privateKeyBytes);
        var publicKey = EncodePublicKey(publicKeyBytes);

        return new EosioKey(privateKeyBytes, publicKeyBytes, privateKeyWif, publicKey, format);
    }

    /// <summary>
    /// Creates key from modern PVT_K1_ format
    /// </summary>
    private static EosioKey FromModernK1(string privateKey)
    {
        var data = privateKey[7..];
        var privateKeyBytes = Base58CheckDecode(data, "K1");
        
        var publicKeyBytes = DerivePublicKey(privateKeyBytes);
        var publicKey = EncodePublicKey(publicKeyBytes);

        return new EosioKey(privateKeyBytes, publicKeyBytes, privateKey, publicKey, KeyFormat.ModernK1);
    }

    /// <summary>
    /// Detects the format of a private key string
    /// </summary>
    public static KeyFormat DetectFormat(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
            return KeyFormat.Unknown;

        var trimmed = privateKey.Trim();

        if (trimmed.StartsWith("PVT_K1_"))
            return KeyFormat.ModernK1;
        
        if (trimmed.StartsWith("PVT_R1_"))
            return KeyFormat.ModernR1;
        
        if (trimmed.StartsWith("5") && trimmed.Length >= 50 && trimmed.Length <= 52)
            return KeyFormat.LegacyWif;

        // Check for hex (with or without 0x prefix)
        var hexPart = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) 
            ? trimmed[2..] 
            : trimmed;
        
        if (hexPart.Length == 64 && IsValidHex(hexPart))
            return KeyFormat.RawHex;

        return KeyFormat.Unknown;
    }

    /// <summary>
    /// Validates a private key string without importing it
    /// </summary>
    public static bool IsValidPrivateKey(string privateKey)
    {
        try
        {
            var format = DetectFormat(privateKey);
            if (format == KeyFormat.Unknown)
                return false;

            // Try to import it
            var _ = FromPrivateKey(privateKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Encodes a private key to WIF format
    /// </summary>
    private static string EncodeWif(byte[] privateKey)
    {
        // WIF format: version (0x80) + privateKey + checksum (4 bytes)
        var version = new byte[] { 0x80 };
        var versionedKey = new byte[version.Length + privateKey.Length];
        Array.Copy(version, 0, versionedKey, 0, version.Length);
        Array.Copy(privateKey, 0, versionedKey, version.Length, privateKey.Length);

        using var sha256 = SHA256.Create();
        var hash1 = sha256.ComputeHash(versionedKey);
        var hash2 = sha256.ComputeHash(hash1);

        var wifData = new byte[versionedKey.Length + 4];
        Array.Copy(versionedKey, 0, wifData, 0, versionedKey.Length);
        Array.Copy(hash2, 0, wifData, versionedKey.Length, 4);

        return Base58Encode(wifData);
    }

    /// <summary>
    /// Signs data with the private key
    /// </summary>
    public byte[] Sign(byte[] data)
    {
        var curve = SecNamedCurves.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        var privateKeyParams = new ECPrivateKeyParameters(new BigInteger(1, _privateKeyBytes), domain);
        
        var signer = new ECDsaSigner();
        signer.Init(true, privateKeyParams);
        var signature = signer.GenerateSignature(data);
        
        // Convert to byte array (r and s values)
        var r = signature[0].ToByteArrayUnsigned();
        var s = signature[1].ToByteArrayUnsigned();
        
        var result = new byte[64];
        Array.Copy(r, 0, result, 32 - r.Length, r.Length);
        Array.Copy(s, 0, result, 64 - s.Length, s.Length);
        
        return result;
    }

    /// <summary>
    /// Signs data and returns compact signature with recovery ID (for EOSIO)
    /// Uses Cryptography.ECDSA library for canonical signatures
    /// </summary>
    public byte[] SignCompact(byte[] data)
    {
        // Use Cryptography.ECDSA library which handles canonical signatures correctly
        return Secp256K1Manager.SignCompressedCompact(data, _privateKeyBytes);
    }

    /// <summary>
    /// Derives public key from private key
    /// </summary>
    private static byte[] DerivePublicKey(byte[] privateKey)
    {
        var curve = SecNamedCurves.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        var privateKeyBigInt = new BigInteger(1, privateKey);
        var q = domain.G.Multiply(privateKeyBigInt);
        return q.GetEncoded(true); // true for compressed format
    }

    /// <summary>
    /// Encodes public key in EOSIO format (EOS...)
    /// </summary>
    private static string EncodePublicKey(byte[] publicKey)
    {
        // EOSIO public key format: EOS + base58(publicKey + checksum)
        using var sha256 = SHA256.Create();
        var hash1 = sha256.ComputeHash(publicKey);
        var hash2 = sha256.ComputeHash(hash1);
        var checksum = new byte[4];
        Array.Copy(hash2, 0, checksum, 0, 4);

        var data = new byte[publicKey.Length + checksum.Length];
        Array.Copy(publicKey, 0, data, 0, publicKey.Length);
        Array.Copy(checksum, 0, data, publicKey.Length, checksum.Length);

        return "EOS" + Base58Encode(data);
    }

    /// <summary>
    /// Encodes public key in modern PUB_K1_ format with RIPEMD160 checksum
    /// </summary>
    public static string EncodePublicKeyK1(byte[] publicKey)
    {
        // Modern format: PUB_K1_ + base58(publicKey + ripemd160(publicKey + "K1")[0:4])
        var ripemd160 = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var suffix = System.Text.Encoding.UTF8.GetBytes("K1");
        
        var toHash = new byte[publicKey.Length + suffix.Length];
        Array.Copy(publicKey, 0, toHash, 0, publicKey.Length);
        Array.Copy(suffix, 0, toHash, publicKey.Length, suffix.Length);
        
        var hash = new byte[ripemd160.GetDigestSize()];
        ripemd160.BlockUpdate(toHash, 0, toHash.Length);
        ripemd160.DoFinal(hash, 0);
        
        var checksum = new byte[4];
        Array.Copy(hash, 0, checksum, 0, 4);

        var data = new byte[publicKey.Length + checksum.Length];
        Array.Copy(publicKey, 0, data, 0, publicKey.Length);
        Array.Copy(checksum, 0, data, publicKey.Length, checksum.Length);

        return "PUB_K1_" + Base58Encode(data);
    }

    /// <summary>
    /// Base58 decode with checksum verification
    /// </summary>
    private static byte[] Base58CheckDecode(string encoded, byte[] version)
    {
        var decoded = Base58Decode(encoded);
        
        // Extract version, data, and checksum
        var dataLength = decoded.Length - version.Length - 4;
        var data = new byte[dataLength];
        var checksum = new byte[4];
        
        Array.Copy(decoded, version.Length, data, 0, dataLength);
        Array.Copy(decoded, decoded.Length - 4, checksum, 0, 4);

        // Verify checksum
        using var sha256 = SHA256.Create();
        var versionData = new byte[version.Length + dataLength];
        Array.Copy(version, 0, versionData, 0, version.Length);
        Array.Copy(data, 0, versionData, version.Length, dataLength);
        
        var hash1 = sha256.ComputeHash(versionData);
        var hash2 = sha256.ComputeHash(hash1);
        
        for (int i = 0; i < 4; i++)
        {
            if (checksum[i] != hash2[i])
                throw new InvalidOperationException("Invalid checksum");
        }

        return data;
    }

    /// <summary>
    /// Base58 decode for EOSIO format (with suffix like K1, R1)
    /// </summary>
    private static byte[] Base58CheckDecode(string encoded, string suffix)
    {
        var decoded = Base58Decode(encoded);
        var suffixBytes = System.Text.Encoding.UTF8.GetBytes(suffix);
        
        // Extract data and checksum
        var dataLength = decoded.Length - 4;
        var data = new byte[dataLength];
        var checksum = new byte[4];
        
        Array.Copy(decoded, 0, data, 0, dataLength);
        Array.Copy(decoded, dataLength, checksum, 0, 4);

        // Verify checksum using RIPEMD160
        var ripemd160 = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var toHash = new byte[data.Length + suffixBytes.Length];
        Array.Copy(data, 0, toHash, 0, data.Length);
        Array.Copy(suffixBytes, 0, toHash, data.Length, suffixBytes.Length);
        
        var hash = new byte[ripemd160.GetDigestSize()];
        ripemd160.BlockUpdate(toHash, 0, toHash.Length);
        ripemd160.DoFinal(hash, 0);
        
        for (int i = 0; i < 4; i++)
        {
            if (checksum[i] != hash[i])
                throw new InvalidOperationException("Invalid checksum");
        }

        return data;
    }

    /// <summary>
    /// Validates if a string contains only valid hex characters
    /// </summary>
    private static bool IsValidHex(string hex)
    {
        foreach (var c in hex)
        {
            if (!((c >= '0' && c <= '9') || 
                  (c >= 'a' && c <= 'f') || 
                  (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Base58 encoding
    /// </summary>
    private static string Base58Encode(byte[] data)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var value = new BigInteger(1, data);
        var result = new List<char>();

        while (value.CompareTo(BigInteger.Zero) > 0)
        {
            var remainder = value.Mod(BigInteger.ValueOf(58));
            value = value.Divide(BigInteger.ValueOf(58));
            result.Insert(0, alphabet[remainder.IntValue]);
        }

        // Add leading zeros
        foreach (var b in data)
        {
            if (b != 0) break;
            result.Insert(0, '1');
        }

        return new string(result.ToArray());
    }

    /// <summary>
    /// Parse a public key from EOS or PUB_K1_ format and return the raw bytes
    /// </summary>
    public static byte[] ParsePublicKey(string publicKey)
    {
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new ArgumentException("Public key cannot be empty", nameof(publicKey));

        publicKey = publicKey.Trim();

        // Handle PUB_K1_ format
        if (publicKey.StartsWith("PUB_K1_"))
        {
            var base58Part = publicKey[7..]; // Remove "PUB_K1_" prefix
            var decoded = Base58Decode(base58Part);
            
            if (decoded.Length < 37) // 33 bytes pubkey + 4 bytes checksum
                throw new FormatException($"Invalid PUB_K1_ public key length: {decoded.Length}");
            
            // Extract public key (all but last 4 bytes)
            var pubKeyBytes = new byte[decoded.Length - 4];
            Array.Copy(decoded, 0, pubKeyBytes, 0, pubKeyBytes.Length);
            
            // Verify checksum
            var checksum = new byte[4];
            Array.Copy(decoded, decoded.Length - 4, checksum, 0, 4);
            
            var ripemd160 = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
            var suffix = System.Text.Encoding.UTF8.GetBytes("K1");
            var toHash = new byte[pubKeyBytes.Length + suffix.Length];
            Array.Copy(pubKeyBytes, 0, toHash, 0, pubKeyBytes.Length);
            Array.Copy(suffix, 0, toHash, pubKeyBytes.Length, suffix.Length);
            
            var hash = new byte[ripemd160.GetDigestSize()];
            ripemd160.BlockUpdate(toHash, 0, toHash.Length);
            ripemd160.DoFinal(hash, 0);
            
            // Verify first 4 bytes match
            for (int i = 0; i < 4; i++)
            {
                if (hash[i] != checksum[i])
                    throw new FormatException("Invalid PUB_K1_ checksum");
            }
            
            return pubKeyBytes;
        }
        // Handle legacy EOS format
        else if (publicKey.StartsWith("EOS"))
        {
            var base58Part = publicKey[3..]; // Remove "EOS" prefix
            var decoded = Base58Decode(base58Part);
            
            if (decoded.Length < 37) // 33 bytes pubkey + 4 bytes checksum
                throw new FormatException($"Invalid EOS public key length: {decoded.Length}");
            
            // Extract public key (all but last 4 bytes)
            var pubKeyBytes = new byte[decoded.Length - 4];
            Array.Copy(decoded, 0, pubKeyBytes, 0, pubKeyBytes.Length);
            
            // Verify checksum
            var checksum = new byte[4];
            Array.Copy(decoded, decoded.Length - 4, checksum, 0, 4);
            
            using var sha256 = SHA256.Create();
            var hash1 = sha256.ComputeHash(pubKeyBytes);
            var hash2 = sha256.ComputeHash(hash1);
            
            for (int i = 0; i < 4; i++)
            {
                if (hash2[i] != checksum[i])
                    throw new FormatException("Invalid EOS public key checksum");
            }
            
            return pubKeyBytes;
        }
        else
        {
            throw new FormatException($"Unknown public key format: {publicKey[..Math.Min(10, publicKey.Length)]}");
        }
    }

    /// <summary>
    /// Base58 decoding
    /// </summary>
    private static byte[] Base58Decode(string encoded)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var value = BigInteger.Zero;

        foreach (var c in encoded)
        {
            var digit = alphabet.IndexOf(c);
            if (digit < 0)
                throw new FormatException($"Invalid Base58 character: {c}");
            value = value.Multiply(BigInteger.ValueOf(58)).Add(BigInteger.ValueOf(digit));
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
        var leadingZeros = 0;
        foreach (var c in encoded)
        {
            if (c != '1') break;
            leadingZeros++;
        }

        if (leadingZeros > 0)
        {
            var withLeadingZeros = new byte[bytes.Length + leadingZeros];
            Array.Copy(bytes, 0, withLeadingZeros, leadingZeros, bytes.Length);
            bytes = withLeadingZeros;
        }

        return bytes;
    }
}
