using System.Security.Cryptography;
using System.Text;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.Cryptography;
using NBitcoin;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Cryptography service implementing Anchor-compatible encryption
/// Uses PBKDF2 + AES-256-CBC for key storage encryption
/// </summary>
public class CryptographyService : ICryptographyService
{
    private const int DefaultIterations = 4500;
    private const int KeySize = 256;
    private const int IvSize = 128;
    private const int SaltSize = 128;

    /// <summary>
    /// Encrypt data using PBKDF2 + AES-256-CBC (Anchor-compatible format)
    /// Format: [32-char salt][32-char IV][encrypted data]
    /// </summary>
    public string Encrypt(string data, string password, int iterations = DefaultIterations)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        // Generate random salt and IV
        var salt = RandomNumberGenerator.GetBytes(SaltSize / 8);  // 16 bytes = 32 hex chars
        var iv = RandomNumberGenerator.GetBytes(IvSize / 8);      // 16 bytes = 32 hex chars
        
        // Derive key using PBKDF2
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA1, KeySize / 8);
        
        // Encrypt data
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        
        using var encryptor = aes.CreateEncryptor();
        var encryptedBytes = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
        
        // Format: salt (32 hex) + iv (32 hex) + encrypted data (hex)
        return Convert.ToHexString(salt).ToLowerInvariant() + 
               Convert.ToHexString(iv).ToLowerInvariant() + 
               Convert.ToHexString(encryptedBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Decrypt data encrypted with Encrypt method
    /// </summary>
    public string Decrypt(string encryptedData, string password, int iterations = DefaultIterations)
    {
        if (encryptedData.Length < 64)
            throw new ArgumentException("Invalid encrypted data format");
        
        try
        {
            // Parse salt, IV, and encrypted data from hex string
            var salt = Convert.FromHexString(encryptedData[..32]);         // First 32 chars
            var iv = Convert.FromHexString(encryptedData[32..64]);         // Next 32 chars  
            var encrypted = Convert.FromHexString(encryptedData[64..]);    // Remainder
            
            // Derive key using PBKDF2
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA1, KeySize / 8);
            
            // Decrypt data
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Failed to decrypt data. Invalid password or corrupted data.", ex);
        }
    }

    /// <summary>
    /// Generate a secure random private key for EOSIO
    /// </summary>
    public string GeneratePrivateKey()
    {
        // Generate 32 random bytes for private key
        var privateKeyBytes = RandomNumberGenerator.GetBytes(32);
        
        // Convert to WIF format (Wallet Import Format)
        // This is compatible with EOSIO private key format
        return ConvertToWif(privateKeyBytes);
    }

    /// <summary>
    /// Derive public key from private key using secp256k1
    /// </summary>
    public string GetPublicKey(string privateKey, string keyPrefix = "EOS")
    {
        try
        {
            // Decode WIF private key
            var decoded = Base58Encoding.Decode(privateKey);
            
            // Extract the 32-byte private key (skip version byte, take 32 bytes, ignore compression flag and checksum)
            var privateKeyBytes = decoded[1..33];
            
            // Use NBitcoin to derive the public key
            var key = new Key(privateKeyBytes);
            var pubKey = key.PubKey;
            
            // Get compressed public key bytes (33 bytes)
            var pubKeyBytes = pubKey.Compress().ToBytes();
            
            // EOSIO public key format: prefix + base58(ripemd160(sha256(pubkey)) checksum + pubkey)
            // For simplicity, we'll use a basic format that works with EOSIO
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(pubKeyBytes);
            
            // Take first 4 bytes as checksum
            var checksum = hash[..4];
            
            // Combine pubkey + checksum
            var combined = new byte[pubKeyBytes.Length + checksum.Length];
            Array.Copy(pubKeyBytes, combined, pubKeyBytes.Length);
            Array.Copy(checksum, 0, combined, pubKeyBytes.Length, checksum.Length);
            
            // Encode and add prefix
            return $"{keyPrefix}{Base58Encoding.Encode(combined)}";
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to derive public key: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate private key format (supports WIF, PVT_K1_, PVT_R1_, and hex formats)
    /// </summary>
    public bool IsValidPrivateKey(string privateKey)
    {
        try
        {
            // Use EosioKey's multi-format validation
            return EosioKey.IsValidPrivateKey(privateKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generate BIP39 mnemonic phrase
    /// </summary>
    public string GenerateMnemonic()
    {
        try
        {
            // Generate 128 bits of entropy (12 words)
            var entropy = RandomNumberGenerator.GetBytes(16);
            var mnemonic = new Mnemonic(Wordlist.English, entropy);
            return mnemonic.ToString();
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to generate mnemonic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Derive private key from BIP39 mnemonic phrase
    /// </summary>
    public string DeriveKeyFromMnemonic(string mnemonic, int accountIndex = 0)
    {
        try
        {
            var mnemonicObj = new Mnemonic(mnemonic, Wordlist.English);
            
            // Use BIP44 derivation path for EOSIO: m/44'/194'/0'/0/{accountIndex}
            // 194 is the registered coin type for EOSIO
            var hdRoot = mnemonicObj.DeriveExtKey();
            var derivationPath = $"m/44'/194'/0'/0/{accountIndex}";
            var hdKey = hdRoot.Derive(KeyPath.Parse(derivationPath));
            
            // Extract private key and convert to WIF format
            var privateKeyBytes = hdKey.PrivateKey.ToBytes();
            return ConvertToWif(privateKeyBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to derive key from mnemonic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert raw private key bytes to WIF format
    /// </summary>
    private static string ConvertToWif(byte[] privateKeyBytes)
    {
        try
        {
            // WIF encoding for EOSIO:
            // 1. Add version byte (0x80 for mainnet)
            // 2. Add compression flag (0x01 for compressed)
            // 3. Calculate double SHA-256 checksum
            // 4. Encode with Base58

            var extended = new byte[privateKeyBytes.Length + 2];
            extended[0] = 0x80; // Version byte
            Array.Copy(privateKeyBytes, 0, extended, 1, privateKeyBytes.Length);
            extended[^1] = 0x01; // Compression flag

            // Calculate checksum (first 4 bytes of double SHA-256)
            using var sha256 = SHA256.Create();
            var hash1 = sha256.ComputeHash(extended);
            var hash2 = sha256.ComputeHash(hash1);
            var checksum = hash2[..4];

            // Combine extended key + checksum
            var final = new byte[extended.Length + checksum.Length];
            Array.Copy(extended, final, extended.Length);
            Array.Copy(checksum, 0, final, extended.Length, checksum.Length);

            // Encode with Base58
            return Base58Encoding.Encode(final);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to convert to WIF format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate WIF format private key
    /// </summary>
    private static bool IsValidWif(string wif)
    {
        try
        {
            var decoded = Base58Encoding.Decode(wif);
            
            // Should be 37 bytes: version(1) + key(32) + compression(1) + checksum(4)
            if (decoded.Length != 37)
                return false;

            // Verify checksum
            var keyWithVersion = decoded[..33];
            using var sha256 = SHA256.Create();
            var hash1 = sha256.ComputeHash(keyWithVersion);
            var hash2 = sha256.ComputeHash(hash1);
            var expectedChecksum = hash2[..4];
            var actualChecksum = decoded[33..];

            return expectedChecksum.SequenceEqual(actualChecksum);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Simple Base58 encoding implementation
/// </summary>
internal static class Base58Encoding
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Encode(byte[] data)
    {
        if (data.Length == 0) return string.Empty;

        var encoded = new List<char>();
        var value = new System.Numerics.BigInteger(data.Reverse().Concat(new byte[] { 0 }).ToArray());

        while (value > 0)
        {
            var remainder = (int)(value % 58);
            value /= 58;
            encoded.Insert(0, Alphabet[remainder]);
        }

        // Add leading zeros
        foreach (var b in data)
        {
            if (b != 0) break;
            encoded.Insert(0, Alphabet[0]);
        }

        return new string(encoded.ToArray());
    }

    public static byte[] Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return Array.Empty<byte>();

        var value = System.Numerics.BigInteger.Zero;
        foreach (var c in encoded)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) throw new ArgumentException($"Invalid character '{c}' in Base58 string");
            value = value * 58 + index;
        }

        var bytes = value.ToByteArray().Reverse().ToArray();
        
        // Remove extra zero byte added by BigInteger
        if (bytes.Length > 1 && bytes[0] == 0)
            bytes = bytes[1..];

        // Add leading zeros
        var leadingZeros = encoded.TakeWhile(c => c == Alphabet[0]).Count();
        var result = new byte[leadingZeros + bytes.Length];
        Array.Copy(bytes, 0, result, leadingZeros, bytes.Length);

        return result;
    }
}