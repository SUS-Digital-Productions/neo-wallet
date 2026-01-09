using System.Text.Json;
using SUS.EOS.NeoWallet.Repositories.Interfaces;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.NeoWallet.Services.Models.WalletData;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Wallet storage service handling wallet.json file operations
/// Implements Anchor-style encrypted wallet storage
/// </summary>
public class WalletStorageService : IWalletStorageService
{
    private readonly ICryptographyService _cryptographyService;
    private readonly INetworkRepository _networkRepository;
    private readonly string _walletFilePath;
    private WalletData? _currentWallet;
    private bool _isUnlocked;
    private Dictionary<string, KeyPair> _unlockedKeys = new();

    // Keep the current unlock password in memory while wallet is unlocked
    // This is cleared on LockWallet for security
    private string? _currentPassword;

    // Diagnostics service for recording important events
    private readonly IAppDiagnosticsService _diagnostics;

    // JSON serialization options for consistent formatting
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public WalletStorageService(
        ICryptographyService cryptographyService,
        INetworkRepository networkRepository,
        SUS.EOS.NeoWallet.Services.Interfaces.IAppDiagnosticsService diagnostics,
        string? walletPath = null
    )
    {
        _cryptographyService = cryptographyService;
        _diagnostics = diagnostics;
        //_networkService = networkService;

        // Default to user's application data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var walletDir = Path.Combine(appDataPath, "SUS.EOS.NeoWallet");
        Directory.CreateDirectory(walletDir);

        _walletFilePath = walletPath ?? Path.Combine(walletDir, "wallet.json");
        _networkRepository = networkRepository;
        _diagnostics.Log("WALLETSERVICE", "WalletStorageService initialized");
    }

    public bool IsUnlocked => _isUnlocked;

    /// <summary>
    /// Load wallet data from wallet.json file
    /// </summary>
    public async Task<WalletData?> LoadWalletAsync()
    {
        try
        {
            if (!File.Exists(_walletFilePath))
                return null;

            var jsonContent = await File.ReadAllTextAsync(_walletFilePath);
            _currentWallet = JsonSerializer.Deserialize<WalletData>(jsonContent, JsonOptions);

            return _currentWallet;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Save wallet data to wallet.json file
    /// </summary>
    public async Task SaveWalletAsync(WalletData walletData)
    {
        try
        {
            // Update metadata
            walletData.Metadata.Updated = DateTime.UtcNow;

            var jsonContent = JsonSerializer.Serialize(walletData, JsonOptions);

            // Create backup of existing wallet before overwriting
            if (File.Exists(_walletFilePath))
            {
                var backupPath = $"{_walletFilePath}.backup.{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                File.Copy(_walletFilePath, backupPath, overwrite: true);

                // Keep only the 5 most recent backups
                await CleanupOldBackups();
            }

            await File.WriteAllTextAsync(_walletFilePath, jsonContent);
            _currentWallet = walletData;
            _diagnostics.Log("WALLETSERVICE", $"Saved wallet to {_walletFilePath}. publicKeys={walletData.Storage?.PublicKeys?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Create a new wallet with default configuration
    /// </summary>
    public async Task<WalletData> CreateWalletAsync(string password, string? description = null)
    {
        try
        {
            var walletData = new WalletData
            {
                Schema = "neowallet.v1.storage",
                Metadata = new WalletMetadata
                {
                    Version = "1.0.0",
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Application = "SUS.EOS.NeoWallet",
                    Description = description,
                },
                Networks = _networkRepository.GetPredefinedNetworks(),
                Storage = new EncryptedStorage
                {
                    EncryptedData = null, // No keys initially
                    PublicKeys = new List<string>(),
                    HardwarePaths = new Dictionary<string, string>(),
                    Encryption = new EncryptionInfo
                    {
                        Algorithm = "AES-256-CBC",
                        KeyDerivation = "PBKDF2",
                        Iterations = 4500,
                        KeySize = 256,
                        IvSize = 128,
                    },
                },
                Wallets = new List<WalletAccount>(),
                Settings = new WalletSettings
                {
                    DefaultNetwork = "wax",
                    AutoLock = true,
                    LockTimeoutMinutes = 15,
                    ShowTestnets = false,
                    Currency = "USD",
                    Language = "en",
                    Theme = "auto",
                },
            };

            // Create empty encrypted storage
            walletData.Storage.EncryptedData = _cryptographyService.Encrypt(
                JsonSerializer.Serialize(new List<KeyPair>(), JsonOptions),
                password
            );

            await SaveWalletAsync(walletData);
            return walletData;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate password against encrypted storage
    /// </summary>
    public async Task<bool> ValidatePasswordAsync(string password)
    {
        try
        {
            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet?.Storage.EncryptedData == null)
                return false;

            // Try to decrypt storage - if successful, password is valid
            var decrypted = _cryptographyService.Decrypt(wallet.Storage.EncryptedData, password);

            // Validate that decrypted content is valid JSON
            JsonSerializer.Deserialize<List<KeyPair>>(decrypted, JsonOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Change wallet encryption password
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            if (!await ValidatePasswordAsync(currentPassword))
                return false;

            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet?.Storage.EncryptedData == null)
                return false;

            // Decrypt with current password
            var decryptedData = _cryptographyService.Decrypt(
                wallet.Storage.EncryptedData,
                currentPassword
            );

            // Re-encrypt with new password
            wallet.Storage.EncryptedData = _cryptographyService.Encrypt(decryptedData, newPassword);

            await SaveWalletAsync(wallet);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to change password: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Check if wallet file exists and is valid
    /// </summary>
    public async Task<bool> WalletExistsAsync()
    {
        try
        {
            if (!File.Exists(_walletFilePath))
                return false;

            var wallet = await LoadWalletAsync();
            return wallet != null && !string.IsNullOrEmpty(wallet.Schema);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Delete wallet and all associated data
    /// </summary>
    public Task DeleteWalletAsync()
    {
        // Lock wallet first
        LockWallet();

        // Delete main wallet file
        if (File.Exists(_walletFilePath))
        {
            File.Delete(_walletFilePath);
        }

        // Delete backup files
        var walletDir = Path.GetDirectoryName(_walletFilePath);
        if (walletDir != null && Directory.Exists(walletDir))
        {
            var backupFiles = Directory.GetFiles(walletDir, "wallet.json.backup.*");
            foreach (var backup in backupFiles)
            {
                try
                {
                    File.Delete(backup);
                }
                catch { }
            }
        }

        // Clear in-memory data
        _currentWallet = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Export wallet for backup (includes all encrypted data)
    /// </summary>
    public async Task<string> ExportWalletAsync(string password)
    {
        try
        {
            if (!await ValidatePasswordAsync(password))
                throw new UnauthorizedAccessException("Invalid password");

            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet == null)
                throw new InvalidOperationException("No wallet loaded");

            // Create export package with metadata
            var exportData = new
            {
                exportedAt = DateTime.UtcNow,
                exportVersion = "1.0.0",
                application = "SUS.EOS.NeoWallet",
                wallet = wallet,
            };

            return JsonSerializer.Serialize(exportData, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Import wallet from backup data
    /// </summary>
    public async Task<WalletData> ImportWalletAsync(string backupData, string password)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<JsonElement>(backupData, JsonOptions);

            WalletData? walletData;

            // Check if this is an export package or raw wallet data
            if (importData.TryGetProperty("wallet", out var walletElement))
            {
                walletData = JsonSerializer.Deserialize<WalletData>(
                    walletElement.GetRawText(),
                    JsonOptions
                );
            }
            else
            {
                walletData = JsonSerializer.Deserialize<WalletData>(backupData, JsonOptions);
            }

            if (walletData == null)
                throw new InvalidOperationException("Invalid backup data format");

            // Validate that we can decrypt the storage with the provided password
            if (walletData.Storage.EncryptedData != null)
            {
                _cryptographyService.Decrypt(walletData.Storage.EncryptedData, password);
            }

            // Update metadata for import
            walletData.Metadata.Updated = DateTime.UtcNow;

            await SaveWalletAsync(walletData);
            return walletData;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lock the wallet (clear in-memory keys)
    /// </summary>
    public void LockWallet()
    {
        _isUnlocked = false;
        _unlockedKeys.Clear();
        _currentPassword = null;

        // Clear sensitive data from memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Unlock the wallet with password
    /// </summary>
    public async Task<bool> UnlockWalletAsync(string password)
    {
        try
        {
            if (!await ValidatePasswordAsync(password))
                return false;

            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet?.Storage.EncryptedData == null)
                return false;

            // Decrypt and load keys into memory
            var decryptedData = _cryptographyService.Decrypt(
                wallet.Storage.EncryptedData,
                password
            );
            var keyPairs =
                JsonSerializer.Deserialize<List<KeyPair>>(decryptedData, JsonOptions)
                ?? new List<KeyPair>();

            _unlockedKeys.Clear();
            foreach (var keyPair in keyPairs)
            {
                _unlockedKeys[keyPair.PublicKey] = keyPair;
            }

            _isUnlocked = true;
            // Store password in memory while unlocked so callers can add/remove keys without re-prompting
            _currentPassword = password;
            _diagnostics.Log("WALLETSERVICE", $"Wallet unlocked; keys loaded={_unlockedKeys.Count}");
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to unlock wallet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get unlocked private key by public key
    /// </summary>
    public string? GetUnlockedPrivateKey(string publicKey)
    {
        if (!_isUnlocked)
            return null;

        return _unlockedKeys.TryGetValue(publicKey, out var keyPair) ? keyPair.PrivateKey : null;
    }

    /// <summary>
    /// Add key to encrypted storage
    /// </summary>
    public async Task<bool> AddKeyToStorageAsync(
        string privateKey,
        string publicKey,
        string password,
        string? label = null
    )
    {
        try
        {
            // Support passing an empty password when wallet is already unlocked
            System.Diagnostics.Trace.WriteLine($"[WALLETSERVICE] AddKeyToStorageAsync starting for publicKey={publicKey[..Math.Min(10, publicKey.Length)]}...");
            _diagnostics.Log("WALLETSERVICE", $"AddKeyToStorageAsync start for {publicKey[..Math.Min(10, publicKey.Length)]}...");

            var effectivePassword = password;
            if (string.IsNullOrWhiteSpace(effectivePassword))
            {
                if (_isUnlocked && !string.IsNullOrEmpty(_currentPassword))
                {
                    effectivePassword = _currentPassword;
                    System.Diagnostics.Trace.WriteLine($"[WALLETSERVICE] Using in-memory unlocked password fallback (no plaintext displayed)");
                    _diagnostics.Log("WALLETSERVICE", "Using in-memory unlocked password fallback");
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"[WALLETSERVICE] No password provided and wallet not unlocked; aborting add.");
                    _diagnostics.Log("WALLETSERVICE", "Add aborted: no password and wallet locked");
                    return false;
                }
            }

            if (!await ValidatePasswordAsync(effectivePassword))
            {
                System.Diagnostics.Trace.WriteLine("[WALLETSERVICE] ValidatePasswordAsync failed in AddKeyToStorageAsync");
                return false;
            }

            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet == null)
            {
                System.Diagnostics.Trace.WriteLine("[WALLETSERVICE] No wallet loaded in AddKeyToStorageAsync");
                return false;
            }

            // Ensure storage object exists
            wallet.Storage ??= new SUS.EOS.NeoWallet.Services.Models.EncryptedStorage();

            // Decrypt existing storage
            var keyPairs = new List<KeyPair>();
            if (wallet.Storage.EncryptedData != null)
            {
                var decryptedData = _cryptographyService.Decrypt(
                    wallet.Storage.EncryptedData,
                    effectivePassword
                );
                keyPairs =
                    JsonSerializer.Deserialize<List<KeyPair>>(decryptedData, JsonOptions)
                    ?? new List<KeyPair>();
            }

            // Remove existing entry with same public key
            keyPairs.RemoveAll(k => k.PublicKey == publicKey);

            // Add new key
            keyPairs.Add(
                new KeyPair
                {
                    PrivateKey = privateKey,
                    PublicKey = publicKey,
                    Label = label,
                }
            );

            // Update storage
            wallet.Storage.EncryptedData = _cryptographyService.Encrypt(
                JsonSerializer.Serialize(keyPairs, JsonOptions),
                effectivePassword
            );

            // Update public keys list
            wallet.Storage.PublicKeys = keyPairs.Select(k => k.PublicKey).Distinct().ToList();

            await SaveWalletAsync(wallet);

            System.Diagnostics.Trace.WriteLine($"[WALLETSERVICE] SaveWalletAsync completed; publicKeysCount={wallet.Storage.PublicKeys?.Count ?? 0}");
            _diagnostics.Log("WALLETSERVICE", $"Saved key, totalPublicKeys={wallet.Storage.PublicKeys?.Count ?? 0}");

            // Update in-memory keys if unlocked
            if (_isUnlocked)
            {
                _unlockedKeys[publicKey] = keyPairs.First(k => k.PublicKey == publicKey);
                System.Diagnostics.Trace.WriteLine($"[WALLETSERVICE] In-memory unlocked keys updated for {publicKey[..Math.Min(10, publicKey.Length)]}...");
                _diagnostics.Log("WALLETSERVICE", $"In-memory unlocked key added: {publicKey[..Math.Min(10, publicKey.Length)]}...");
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add key to storage: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Remove key from encrypted storage
    /// </summary>
    public async Task<bool> RemoveKeyFromStorageAsync(string publicKey, string password)
    {
        try
        {
            // Support empty password when wallet is unlocked
            var effectivePassword = password;
            if (string.IsNullOrWhiteSpace(effectivePassword))
            {
                if (_isUnlocked && !string.IsNullOrEmpty(_currentPassword))
                {
                    effectivePassword = _currentPassword;
                }
                else
                {
                    return false;
                }
            }

            _diagnostics.Log("WALLETSERVICE", $"RemoveKeyFromStorageAsync start for {publicKey[..Math.Min(10, publicKey.Length)]}...");
            if (!await ValidatePasswordAsync(effectivePassword))
                return false;

            var wallet = _currentWallet ?? await LoadWalletAsync();
            if (wallet?.Storage.EncryptedData == null)
                return false;

            // Decrypt existing storage
            var decryptedData = _cryptographyService.Decrypt(
                wallet.Storage.EncryptedData,
                effectivePassword
            );
            _diagnostics.Log("WALLETSERVICE", $"Key removal decrypted storage; publicKeysBefore={wallet.Storage.PublicKeys?.Count ?? 0}");
            var keyPairs =
                JsonSerializer.Deserialize<List<KeyPair>>(decryptedData, JsonOptions)
                ?? new List<KeyPair>();

            // Remove key
            keyPairs.RemoveAll(k => k.PublicKey == publicKey);

            // Update storage
            wallet.Storage.EncryptedData = _cryptographyService.Encrypt(
                JsonSerializer.Serialize(keyPairs, JsonOptions),
                effectivePassword
            );

            // Update public keys list
            wallet.Storage.PublicKeys = keyPairs.Select(k => k.PublicKey).Distinct().ToList();

            await SaveWalletAsync(wallet);

            // Update in-memory keys if unlocked
            if (_isUnlocked)
            {
                _unlockedKeys.Remove(publicKey);
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to remove key from storage: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Clean up old backup files
    /// </summary>
    private async Task CleanupOldBackups()
    {
        try
        {
            var backupFiles = Directory
                .GetFiles(
                    Path.GetDirectoryName(_walletFilePath) ?? "",
                    Path.GetFileName(_walletFilePath) + ".backup.*"
                )
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();

            // Keep only the 5 most recent backups
            for (int i = 5; i < backupFiles.Length; i++)
            {
                File.Delete(backupFiles[i]);
            }
        }
        catch
        {
            // Ignore cleanup errors - they're not critical
        }
    }
}
