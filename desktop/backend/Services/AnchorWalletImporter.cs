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
        var trimmed = text.Trim().TrimStart('\uFEFF');

        // Pull out every plausible (salt, iv, ciphertext) candidate from the backup.
        var envelopes = ExtractEnvelopes(trimmed);
        if (envelopes.Count == 0) return null;

        // Try several KDFs in order of likelihood.
        // Anchor/Scatter default: PBKDF2-SHA1, 4500 iterations (CryptoJS default).
        // Some newer builds use higher iteration counts.
        string[] kdfs = ["pbkdf2-sha1-4500-cryptojs", "pbkdf2-sha1-4500", "pbkdf2-sha1-10k", "pbkdf2-sha256-100k", "pbkdf2-sha256-10k", "pbkdf2-sha256-50k", "pbkdf2-sha512-10k", "pbkdf2-sha512-100k", "scrypt-16384-8-1"];

        foreach (var (saltBytes, ivBytes, cipherBytes, format) in envelopes)
        {
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
        }

        System.Diagnostics.Trace.WriteLine(
            $"[ANCHOR-IMPORT] Decryption failed for {envelopes.Count} envelope candidate(s)");
        return null;
    }

    // ------------------------------------------------------------------
    // Envelope detection
    // ------------------------------------------------------------------
    private static List<(byte[] salt, byte[] iv, byte[] cipher, string format)>
        ExtractEnvelopes(string text)
    {
        var candidates = new List<(byte[] salt, byte[] iv, byte[] cipher, string format)>();

        // Try JSON object first.
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                CollectJsonEnvelopeCandidates(root, "$", candidates);
            }
        }
        catch
        {
            // not JSON
        }

        var anchorBlob = TryParseAnchorV2Blob(text);
        if (anchorBlob is not null)
            AddCandidate(candidates, anchorBlob.Value.salt, anchorBlob.Value.iv, anchorBlob.Value.cipher, anchorBlob.Value.format);

        // Try a single hex/base64 blob laid out as salt[16] | iv[16] | ciphertext.
        var blob = TryDecodeBytes(text);
        if (blob is not null && blob.Length > 32)
        {
            var salt = blob[..16];
            var iv = blob[16..32];
            var cipher = blob[32..];
            AddCandidate(candidates, salt, iv, cipher, "blob:concat");
        }

        return candidates;
    }

    private static void CollectJsonEnvelopeCandidates(
        JsonElement element,
        string path,
        List<(byte[] salt, byte[] iv, byte[] cipher, string format)> candidates)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var fields = TryReadFields(element);
                if (fields is not null)
                    AddCandidate(candidates, fields.Value.salt, fields.Value.iv, fields.Value.cipher, $"json:{path}");

                foreach (var property in element.EnumerateObject())
                    CollectJsonEnvelopeCandidates(property.Value, $"{path}.{property.Name}", candidates);
                break;

            case JsonValueKind.Array:
                var arrayIndex = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonEnvelopeCandidates(item, $"{path}[{arrayIndex}]", candidates);
                    arrayIndex++;
                }
                break;

            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var parsed = TryParseAnchorV2Blob(value.Trim());
                    if (parsed is not null)
                        AddCandidate(candidates, parsed.Value.salt, parsed.Value.iv, parsed.Value.cipher, $"{parsed.Value.format}:{path}");
                }
                break;
        }
    }

    private static void AddCandidate(
        List<(byte[] salt, byte[] iv, byte[] cipher, string format)> candidates,
        byte[] salt,
        byte[] iv,
        byte[] cipher,
        string format)
    {
        if (salt.Length == 0 || iv.Length != 16 || cipher.Length == 0) return;
        if (candidates.Any(candidate =>
                candidate.format == format &&
                candidate.salt.SequenceEqual(salt) &&
                candidate.iv.SequenceEqual(iv) &&
                candidate.cipher.SequenceEqual(cipher)))
            return;

        candidates.Add((salt, iv, cipher, format));
    }

    /// <summary>
    /// Parses the Anchor/CryptoJS encrypted blob.
    ///
    /// CryptoJS AES encrypt output format (from wallet.js):
    ///   saltHex(32 chars = 16 bytes) + ivHex(32 chars = 16 bytes) + base64(ciphertext)
    ///
    /// Fallback: treat as pure base64 with salt/IV as leading bytes.
    /// </summary>
    private static (byte[] salt, byte[] iv, byte[] cipher, string format)?
        TryParseAnchorV2Blob(string blobStr)
    {
        // Primary: salt(32 hex) + iv(32 hex) + base64(cipher)
        // Total hex prefix = 64 chars (32 salt + 32 iv)
        if (blobStr.Length > 64)
        {
            var saltHex = blobStr[..32];
            var ivHex = blobStr[32..64];
            var b64Part = blobStr[64..];

            bool saltOk = Regex.IsMatch(saltHex, "^[0-9a-fA-F]+$");
            bool ivOk = Regex.IsMatch(ivHex, "^[0-9a-fA-F]+$");

            if (saltOk && ivOk)
            {
                try
                {
                    var saltBytes = Convert.FromHexString(saltHex);
                    var ivBytes = Convert.FromHexString(ivHex);
                    var cipherBytes = TryDecodeBase64(b64Part);
                    if (cipherBytes is not null && cipherBytes.Length > 0)
                        return (saltBytes, ivBytes, cipherBytes, "anchor:saltHex-ivHex-b64");
                }
                catch { /* fall through */ }
            }
        }

        // Legacy fallback: hexSalt + base64(iv[16] + cipher)
        int hexLen = 0;
        for (int i = 0; i < blobStr.Length; i++)
        {
            char c = blobStr[i];
            if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                hexLen++;
            else
                break;
        }
        hexLen = hexLen % 2 == 0 ? hexLen : hexLen - 1;

        if (hexLen >= 32 && hexLen <= 64)
        {
            int[] saltLengths = hexLen >= 64 ? [64, 32] : hexLen >= 32 ? [32] : [];
            foreach (var sLen in saltLengths)
            {
                var saltHex = blobStr[..sLen];
                var b64Part = blobStr[sLen..];

                byte[] saltBytes;
                try { saltBytes = Convert.FromHexString(saltHex); }
                catch { continue; }

                var decoded = TryDecodeBase64(b64Part);
                if (decoded is null || decoded.Length < 32) continue;

                var iv = decoded[..16];
                var cipher = decoded[16..];
                return (saltBytes, iv, cipher, $"anchor-v2:hex{sLen}-b64");
            }
        }

        // Fallback: entire string is base64; first N bytes = salt, next 16 = IV.
        var fullBlob = TryDecodeBase64(blobStr);
        if (fullBlob is not null && fullBlob.Length > 48)
        {
            return (fullBlob[..32], fullBlob[32..48], fullBlob[48..], "anchor-v2:b64-concat-32");
        }
        if (fullBlob is not null && fullBlob.Length > 32)
        {
            return (fullBlob[..16], fullBlob[16..32], fullBlob[32..], "anchor-v2:b64-concat-16");
        }

        return null;
    }

    private static byte[]? TryDecodeBase64(string s)
    {
        // Handle both standard and URL-safe base64, with or without padding.
        var padded = s.Replace('-', '+').Replace('_', '/');
        var rem = padded.Length % 4;
        if (rem == 2) padded += "==";
        else if (rem == 3) padded += "=";
        try { return Convert.FromBase64String(padded); }
        catch { return null; }
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
            // Anchor default: PBKDF2-SHA1, 4500 iterations, keySize=64 words (256 bytes in CryptoJS)
            // CryptoJS AES uses the full 64-word key schedule, so support both that and standard AES-256.
            "pbkdf2-sha1-4500-cryptojs" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 4_500, HashAlgorithmName.SHA1, 256),
            "pbkdf2-sha1-4500" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 4_500, HashAlgorithmName.SHA1, 32),
            "pbkdf2-sha1-10k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 10_000, HashAlgorithmName.SHA1, 32),
            "pbkdf2-sha256-100k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA256, 32),
            "pbkdf2-sha256-50k" => Rfc2898DeriveBytes.Pbkdf2(pw, salt, 50_000, HashAlgorithmName.SHA256, 32),
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
        if (key.Length is not (16 or 24 or 32))
            return CryptoJsAesCbcDecrypt(cipher, key, iv);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static byte[] CryptoJsAesCbcDecrypt(byte[] cipher, byte[] key, byte[] iv)
    {
        if (cipher.Length == 0 || cipher.Length % 16 != 0)
            throw new CryptographicException("Ciphertext length must be a positive multiple of 16 bytes.");
        if (key.Length == 0 || key.Length % 4 != 0)
            throw new CryptographicException("CryptoJS AES key length must be a positive multiple of 4 bytes.");

        var schedule = CryptoJsAes.CreateKeySchedule(key);
        var plaintext = new byte[cipher.Length];
        var previousBlock = iv.ToArray();

        for (var blockOffset = 0; blockOffset < cipher.Length; blockOffset += 16)
        {
            var cipherBlock = cipher[blockOffset..(blockOffset + 16)];
            var decryptedBlock = CryptoJsAes.DecryptBlock(cipherBlock, schedule);

            for (var byteIndex = 0; byteIndex < 16; byteIndex++)
                plaintext[blockOffset + byteIndex] = (byte)(decryptedBlock[byteIndex] ^ previousBlock[byteIndex]);

            previousBlock = cipherBlock;
        }

        return RemovePkcs7Padding(plaintext);
    }

    private static byte[] RemovePkcs7Padding(byte[] plaintext)
    {
        var paddingLength = plaintext[^1];
        if (paddingLength is < 1 or > 16 || paddingLength > plaintext.Length)
            throw new CryptographicException("Invalid PKCS7 padding.");

        for (var paddingIndex = plaintext.Length - paddingLength; paddingIndex < plaintext.Length; paddingIndex++)
        {
            if (plaintext[paddingIndex] != paddingLength)
                throw new CryptographicException("Invalid PKCS7 padding.");
        }

        return plaintext[..^paddingLength];
    }

    private sealed record CryptoJsAesKeySchedule(int RoundCount, uint[] RoundKeys);

    private static class CryptoJsAes
    {
        private static readonly byte[] SBox =
        [
            0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76,
            0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0,
            0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15,
            0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75,
            0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84,
            0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf,
            0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8,
            0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2,
            0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73,
            0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb,
            0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79,
            0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08,
            0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a,
            0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e,
            0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf,
            0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16
        ];

        private static readonly byte[] InvSBox =
        [
            0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb,
            0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb,
            0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e,
            0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25,
            0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92,
            0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84,
            0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06,
            0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b,
            0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73,
            0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e,
            0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b,
            0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4,
            0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f,
            0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef,
            0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61,
            0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d
        ];

        private static readonly byte[] Rcon = [0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36];

        public static CryptoJsAesKeySchedule CreateKeySchedule(byte[] key)
        {
            var keyWordCount = key.Length / 4;
            var roundCount = keyWordCount + 6;
            var roundKeyCount = (roundCount + 1) * 4;
            var roundKeys = new uint[roundKeyCount];

            for (var wordIndex = 0; wordIndex < keyWordCount; wordIndex++)
                roundKeys[wordIndex] = ReadUInt32BigEndian(key, wordIndex * 4);

            for (var wordIndex = keyWordCount; wordIndex < roundKeyCount; wordIndex++)
            {
                var tempWord = roundKeys[wordIndex - 1];
                if (wordIndex % keyWordCount == 0)
                {
                    var rconIndex = wordIndex / keyWordCount;
                    tempWord = SubWord(RotWord(tempWord)) ^ ((uint)Rcon[rconIndex] << 24);
                }
                else if (keyWordCount > 6 && wordIndex % keyWordCount == 4)
                {
                    tempWord = SubWord(tempWord);
                }

                roundKeys[wordIndex] = roundKeys[wordIndex - keyWordCount] ^ tempWord;
            }

            return new CryptoJsAesKeySchedule(roundCount, roundKeys);
        }

        public static byte[] DecryptBlock(byte[] cipherBlock, CryptoJsAesKeySchedule schedule)
        {
            var state = cipherBlock.ToArray();

            AddRoundKey(state, schedule.RoundKeys, schedule.RoundCount);
            for (var round = schedule.RoundCount - 1; round >= 1; round--)
            {
                InvShiftRows(state);
                InvSubBytes(state);
                AddRoundKey(state, schedule.RoundKeys, round);
                InvMixColumns(state);
            }

            InvShiftRows(state);
            InvSubBytes(state);
            AddRoundKey(state, schedule.RoundKeys, 0);

            return state;
        }

        private static void AddRoundKey(byte[] state, uint[] roundKeys, int round)
        {
            for (var column = 0; column < 4; column++)
            {
                var roundKey = roundKeys[(round * 4) + column];
                state[column * 4] ^= (byte)(roundKey >> 24);
                state[(column * 4) + 1] ^= (byte)(roundKey >> 16);
                state[(column * 4) + 2] ^= (byte)(roundKey >> 8);
                state[(column * 4) + 3] ^= (byte)roundKey;
            }
        }

        private static void InvShiftRows(byte[] state)
        {
            var original = state.ToArray();
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var sourceColumn = (column - row + 4) % 4;
                    state[row + (4 * column)] = original[row + (4 * sourceColumn)];
                }
            }
        }

        private static void InvSubBytes(byte[] state)
        {
            for (var byteIndex = 0; byteIndex < state.Length; byteIndex++)
                state[byteIndex] = InvSBox[state[byteIndex]];
        }

        private static void InvMixColumns(byte[] state)
        {
            for (var column = 0; column < 4; column++)
            {
                var columnOffset = column * 4;
                var first = state[columnOffset];
                var second = state[columnOffset + 1];
                var third = state[columnOffset + 2];
                var fourth = state[columnOffset + 3];

                state[columnOffset] = (byte)(Multiply(first, 14) ^ Multiply(second, 11) ^ Multiply(third, 13) ^ Multiply(fourth, 9));
                state[columnOffset + 1] = (byte)(Multiply(first, 9) ^ Multiply(second, 14) ^ Multiply(third, 11) ^ Multiply(fourth, 13));
                state[columnOffset + 2] = (byte)(Multiply(first, 13) ^ Multiply(second, 9) ^ Multiply(third, 14) ^ Multiply(fourth, 11));
                state[columnOffset + 3] = (byte)(Multiply(first, 11) ^ Multiply(second, 13) ^ Multiply(third, 9) ^ Multiply(fourth, 14));
            }
        }

        private static byte Multiply(byte value, byte factor)
        {
            byte result = 0;
            var currentValue = value;
            var currentFactor = factor;

            while (currentFactor > 0)
            {
                if ((currentFactor & 1) != 0)
                    result ^= currentValue;

                var highBitSet = (currentValue & 0x80) != 0;
                currentValue <<= 1;
                if (highBitSet)
                    currentValue ^= 0x1b;

                currentFactor >>= 1;
            }

            return result;
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) |
            ((uint)bytes[offset + 1] << 16) |
            ((uint)bytes[offset + 2] << 8) |
            bytes[offset + 3];

        private static uint RotWord(uint word) => (word << 8) | (word >> 24);

        private static uint SubWord(uint word) =>
            ((uint)SBox[(word >> 24) & 0xff] << 24) |
            ((uint)SBox[(word >> 16) & 0xff] << 16) |
            ((uint)SBox[(word >> 8) & 0xff] << 8) |
            SBox[word & 0xff];
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
