using System.Security.Cryptography;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Serialization;
using Org.BouncyCastle.Math;

namespace SUS.EOS.Sharp.Signatures;

/// <summary>
/// EOSIO signature provider using secp256k1
/// </summary>
public sealed class EosioSignatureProvider
{
    private readonly EosioKey _key;

    /// <summary>
    /// Construct a signature provider from a private key in WIF format
    /// </summary>
    /// <param name="privateKeyWif">Private key in WIF format</param>
    public EosioSignatureProvider(string privateKeyWif)
    {
        _key = EosioKey.FromWif(privateKeyWif);
    }

    /// <summary>
    /// Public key corresponding to the private key (EOSIO format)
    /// </summary>
    public string PublicKey => _key.PublicKey;

    /// <summary>
    /// Signs a raw SHA256 digest and returns EOSIO formatted signature
    /// Used for identity proofs and other non-transaction signatures
    /// </summary>
    public string SignDigest(byte[] digest)
    {
        // Sign with Cryptography.ECDSA which produces canonical signatures
        var compactSignature = _key.SignCompact(digest);
        
        // Encode in EOSIO format
        return EncodeSignature(compactSignature);
    }

    /// <summary>
    /// Signs a transaction and returns EOSIO formatted signature
    /// </summary>
    public string SignTransaction<T>(string chainId, EosioTransaction<T> transaction)
    {
        // Serialize transaction
        var serialized = EosioSerializer.SerializeTransaction(transaction);
        
        // Create signing data (chainId + transaction + 32 zeros)
        var signingData = EosioSerializer.CreateSigningData(chainId, serialized);
        
        // Hash the signing data with SHA256 - this is what EOSIO signs
        var hash = EosioSerializer.Sha256(signingData);
        
        // Sign with Cryptography.ECDSA which produces canonical signatures
        // The library expects the hash, not raw data
        var compactSignature = _key.SignCompact(hash);
        
        // Encode in EOSIO format
        return EncodeSignature(compactSignature);
    }

    /// <summary>
    /// Encodes signature in EOSIO format (SIG_K1_...)
    /// The Secp256K1Manager.SignCompressedCompact already returns the signature
    /// with the correct recovery ID format for EOSIO
    /// </summary>
    private static string EncodeSignature(byte[] compactSignature)
    {
        const string keyType = "K1";
        
        // Calculate checksum using signature + "K1" suffix
        var checksum = CalculateChecksum(compactSignature, keyType);
        
        // Combine signature + checksum
        var data = new byte[compactSignature.Length + checksum.Length];
        Array.Copy(compactSignature, 0, data, 0, compactSignature.Length);
        Array.Copy(checksum, 0, data, compactSignature.Length, checksum.Length);
        
        // Base58 encode
        var encoded = Base58Encode(data);
        
        return $"SIG_{keyType}_{encoded}";
    }

    /// <summary>
    /// Calculates RIPEMD160 checksum for signature
    /// </summary>
    private static byte[] CalculateChecksum(byte[] data, string suffix)
    {
        var ripemd160 = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var suffixBytes = System.Text.Encoding.UTF8.GetBytes(suffix);
        
        var toHash = new byte[data.Length + suffixBytes.Length];
        Array.Copy(data, 0, toHash, 0, data.Length);
        Array.Copy(suffixBytes, 0, toHash, data.Length, suffixBytes.Length);
        
        var hash = new byte[ripemd160.GetDigestSize()];
        ripemd160.BlockUpdate(toHash, 0, toHash.Length);
        ripemd160.DoFinal(hash, 0);
        
        var checksum = new byte[4];
        Array.Copy(hash, 0, checksum, 0, 4);
        return checksum;
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
}
