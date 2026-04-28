using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Generators;
using SUS.EOS.Sharp.Cryptography;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Result of attempting to decrypt a foreign (non-NeoWallet) wallet file.
/// </summary>
public sealed record AnchorImportResult(string Format, IReadOnlyList<string> PrivateKeysWif);

/// <summary>
/// Best-effort decryptor for "Anchor-style" encrypted wallet backup files.
///
/// There is no single canonical format — different tools (Greymass Anchor desktop,
/// anchor-link softkey, scatter exports, etc.) all use AES-CBC over a JSON
/// envelope containing a salt, an IV and ciphertext, but they differ in:
///   • field names      (data/ciphertext/encrypted, iv/IV, salt/s)
///   • encoding         (hex / base64)
///   • KDF              (PBKDF2-SHA256 / PBKDF2-SHA512 / scrypt N=16384 r=8 p=1)
///   • iteration count  (10_000 / 100_000)
///
/// We try each combination until one yields a plaintext containing recognisable
/// EOSIO WIF private keys. If none works we report which envelope shape we
/// detected so the user can supply a sample for a precise integration.
/// </summary>
public static class AnchorWalletImporter
{
    // EOSIO legacy WIF (starts with 5, K, or L). Length 51.
    private static readonly Regex WifRegex = new(
        @"\b[5KL][1-9A-HJ-NP-Za-km-z]{50,51}\b",
        RegexOptions.Compiled);

    // PVT_K1_... (base58check) — Antelope native private key format.
    private static readonly Regex PvtRegex = new(
        @"\bPVT_K1_[1-9A-HJ-NP-Za-km-z]{40,80}\b",
        RegexOptions.Compiled);

    public static AnchorImportResult? TryImport(byte[] fileBytes, string password)
    {
        string text;
        try { text = Encoding.UTF8.GetString(fileBytes); }
        catch { return null; }

        // Some exports are pure ciphertext (single hex/base64 string).
        var trimmed = text.Trim();

        // Pull out (salt, iv, ciphertext) candidates from the envelope.
        var envelope = ExtractEnvelope(trimmed);
        if (envelope is null) return null;

        var (saltBytes, ivBytes, cipherBytes, format) = envelope.Value;

        // Try several KDFs in order of likelihood.
        string[] kdfs = ["pbkdf2-sha512-100k", "pbkdf2-sha256-100k", "pbkdf2-sha512-10k", "pbkdf2-sha256-10k", "scrypt-16384-8-1"];

        foreach (var kdf in kdfs)
        {
            byte[] key;
            try { key = DeriveKey(kdf, password, saltBytes); }
            catch { continue; }

            byte[] plaintext;
            try { plaintext = AesCbcDecrypt(cipherBytes, key, ivBytes); }
            catch { continue; }

            var keys = ExtractPrivateKeys(plaintext);
            if (keys.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[ANCHOR-IMPORT] Decrypted using {format} + {kdf}; recovered {keys.Count} key(s)");
                return new AnchorImportResult($"{format} + {kdf}", keys);
            }
        }

        System.Diagnostics.Trace.WriteLine(
            $"[ANCHOR-IMPORT] Decryption failed for envelope shape {format}");
        return null;
    }

    // ------------------------------------------------------------------
    // Envelope detection
    // ------------------------------------------------------------------
    private static (byte[] salt, byte[] iv, byte[] cipher, string format)?
        ExtractEnvelope(string text)
    {
        // Try JSON object first.
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            // Walk into "wallet" / "data" sub-objects that some tools wrap with.
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var inner in new[] { "wallet", "data", "encrypted" })
                {
                    if (root.TryGetProperty(inner, out var sub) && sub.ValueKind == JsonValueKind.Object)
                    {
                        var nested = TryReadFields(sub);
                        if (nested is not null) return (nested.Value.salt, nested.Value.iv, nested.Value.cipher, $"json:{inner}");
                    }
                }

                var top = TryReadFields(root);
                if (top is not null) return (top.Value.salt, top.Value.iv, top.Value.cipher, "json:root");
            }
        }
        catch
        {
            // not JSON
        }

        // Try a single hex/base64 blob laid out as salt[16] | iv[16] | ciphertext.
        var blob = TryDecodeBytes(text);
        if (blob is not null && blob.Length > 32)
        {
            var salt = blob[..16];
            var iv = blob[16..32];
            var cipher = blob[32..];
            return (salt, iv, cipher, "blob:concat");
        }

        return null;
    }

    private static (byte[] salt, byte[] iv, byte[] cipher)? TryReadFields(JsonElement obj)
    {
        // Field-name aliases used in the wild.
        string?[] saltNames = ["salt", "s"];
        string?[] ivNames = ["iv", "IV", "nonce"];
        string?[] cipherNames = ["data", "ciphertext", "ct", "encrypted", "value"];

        var salt = ReadFirst(obj, saltNames);
        var iv = ReadFirst(obj, ivNames);
        var cipher = ReadFirst(obj, cipherNames);

        if (salt is null || iv is null || cipher is null) return null;
        return (salt, iv, cipher);
    }

    private static byte[]? ReadFirst(JsonElement obj, IEnumerable<string?> names)
    {
        foreach (var n in names)
        {
            if (n is null) continue;
            if (obj.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (string.IsNullOrEmpty(s)) continue;
                var bytes = TryDecodeBytes(s);
                if (bytes is not null) return bytes;
            }
        }
        return null;
    }

    private static byte[]? TryDecodeBytes(string s)
    {
        s = s.Trim().Trim('"');
        // Hex
        if (s.Length % 2 == 0 && Regex.IsMatch(s, "^[0-9a-fA-F]+$"))
        {
            try { return Convert.FromHexString(s); } catch { /* fall through */ }
        }
        // Base64
        try { return Convert.FromBase64String(s); }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // KDFs
    // ------------------------------------------------------------------
    private static byte[] DeriveKey(string kdf, string password, byte[] salt)
    {
        var pw = Encoding.UTF8.GetBytes(password);
        return kdf switch
        {
            "pbkdf2-sha256-100k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA256, 32),
            "pbkdf2-sha256-10k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 10_000, HashAlgorithmName.SHA256, 32),
            "pbkdf2-sha512-100k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA512, 32),
            "pbkdf2-sha512-10k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 10_000, HashAlgorithmName.SHA512, 32),
            "scrypt-16384-8-1" => SCrypt.Generate(pw, salt, 16384, 8, 1, 32),
            _ => throw new NotSupportedException(kdf)
        };
    }

    // ------------------------------------------------------------------
    // AES-256-CBC
    // ------------------------------------------------------------------
    private static byte[] AesCbcDecrypt(byte[] cipher, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    // ------------------------------------------------------------------
    // Plaintext key extraction
    // ------------------------------------------------------------------
    private static List<string> ExtractPrivateKeys(byte[] plaintext)
    {
        var found = new List<string>();
        string text;
        try { text = Encoding.UTF8.GetString(plaintext); }
        catch { return found; }

        foreach (Match m in WifRegex.Matches(text))
        {
            if (TryValidate(m.Value, out var wif) && !found.Contains(wif))
                found.Add(wif);
        }
        foreach (Match m in PvtRegex.Matches(text))
        {
            if (TryValidate(m.Value, out var wif) && !found.Contains(wif))
                found.Add(wif);
        }
        return found;
    }

    private static bool TryValidate(string candidate, out string wif)
    {
        wif = "";
        try
        {
            // EosioKey throws if the format / checksum is invalid.
            var key = EosioKey.FromPrivateKey(candidate);
            wif = key.PrivateKeyWif;
            return !string.IsNullOrEmpty(wif);
        }
        catch
        {
            return false;
        }
    }
}
