namespace SUS.EOS.NeoWallet.Services.Models;

/// <summary>
/// Decrypted keypair for in-memory use only (never serialized to JSON)
/// </summary>
public class KeyPair
{
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string? Label { get; set; }
}
