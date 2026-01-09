namespace SUS.EOS.NeoWallet.Services.Utils;

public static class PublicKeyUtils
{
    /// <summary>
    /// Compare two EOSIO public key strings ignoring final checksum differences.
    /// This handles keys encoded with different prefixes/encoding variants.
    /// </summary>
    public static bool KeysMatchIgnoringChecksum(string? key1, string? key2)
    {
        if (key1 == key2) return true;
        if (string.IsNullOrEmpty(key1) || string.IsNullOrEmpty(key2)) return false;

        // Strip known prefixes
        var k1 = key1.Replace("EOS", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PUB_K1_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PUB_R1_", "", StringComparison.OrdinalIgnoreCase);
        var k2 = key2.Replace("EOS", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PUB_K1_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PUB_R1_", "", StringComparison.OrdinalIgnoreCase);

        if (Math.Abs(k1.Length - k2.Length) > 5) return false;

        var compareLength = Math.Min(k1.Length, k2.Length) - 5;
        if (compareLength <= 0) return false;

        return k1[..compareLength] == k2[..compareLength];
    }
}