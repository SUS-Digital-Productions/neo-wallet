namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Cryptographic operations service
/// Handles encryption, decryption, and key derivation
/// </summary>
public interface ICryptographyService
{
    /// <summary>
    /// Encrypt data using password with PBKDF2 + AES-256-CBC (Anchor-compatible)
    /// </summary>
    string Encrypt(string data, string password, int iterations = 4500);

    /// <summary>
    /// Decrypt data using password
    /// </summary>
    string Decrypt(string encryptedData, string password, int iterations = 4500);

    /// <summary>
    /// Generate a secure random private key
    /// </summary>
    string GeneratePrivateKey();

    /// <summary>
    /// Derive public key from private key
    /// </summary>
    string GetPublicKey(string privateKey, string keyPrefix = "EOS");

    /// <summary>
    /// Validate private key format
    /// </summary>
    bool IsValidPrivateKey(string privateKey);

    /// <summary>
    /// Generate secure random mnemonic phrase
    /// </summary>
    string GenerateMnemonic();

    /// <summary>
    /// Derive private key from mnemonic phrase
    /// </summary>
    string DeriveKeyFromMnemonic(string mnemonic, int accountIndex = 0);
}
