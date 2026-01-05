using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Providers;

namespace SUS.EOS.Sharp;

/// <summary>
/// Main EOS client for interacting with EOS blockchains
/// </summary>
public sealed class EosClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly EosConfiguration _configuration;
    private readonly IAbiSerializationProvider? _abiSerializer;
    private readonly ISignatureProvider? _signatureProvider;
    private bool _disposed;

    /// <summary>
    /// Creates a new EOS client instance
    /// </summary>
    /// <param name="configuration">Client configuration</param>
    /// <param name="httpClient">Optional HTTP client (will be disposed with this instance)</param>
    /// <param name="abiSerializer">Optional ABI serialization provider</param>
    /// <param name="signatureProvider">Optional signature provider for signing transactions</param>
    public EosClient(
        EosConfiguration configuration,
        HttpClient? httpClient = null,
        IAbiSerializationProvider? abiSerializer = null,
        ISignatureProvider? signatureProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(configuration.HttpEndpoint);
        _httpClient.Timeout = configuration.HttpTimeout;
        _abiSerializer = abiSerializer;
        _signatureProvider = signatureProvider;
    }

    #region Chain API Methods

    /// <summary>
    /// Gets blockchain information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chain information</returns>
    public Task<ChainInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // TODO: Implement actual API call
        throw new NotImplementedException("API integration pending");
    }

    /// <summary>
    /// Gets account information
    /// </summary>
    /// <param name="accountName">Account name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Account information</returns>
    public Task<Account> GetAccountAsync(string accountName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        // TODO: Implement actual API call
        throw new NotImplementedException("API integration pending");
    }

    /// <summary>
    /// Gets account balance for a specific token
    /// </summary>
    /// <param name="accountName">Account name</param>
    /// <param name="tokenContract">Token contract (e.g., "eosio.token")</param>
    /// <param name="symbol">Token symbol (e.g., "EOS")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Account balance</returns>
    public Task<Asset?> GetBalanceAsync(
        string accountName,
        string tokenContract,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        // TODO: Implement actual API call
        throw new NotImplementedException("API integration pending");
    }

    #endregion

    #region Transaction Methods

    /// <summary>
    /// Creates and signs a transaction
    /// </summary>
    /// <param name="transaction">Transaction to sign</param>
    /// <param name="requiredKeys">Optional list of required keys (auto-detected if null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Signed transaction</returns>
    public async Task<SignedTransaction> SignTransactionAsync(
        Transaction transaction,
        IEnumerable<string>? requiredKeys = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(transaction);

        if (_abiSerializer is null)
            throw new InvalidOperationException("ABI serialization provider is required for signing transactions");

        if (_signatureProvider is null)
            throw new InvalidOperationException("Signature provider is required for signing transactions");

        // Serialize transaction
        var packedTransaction = await _abiSerializer.SerializeTransactionAsync(transaction, cancellationToken);

        // Get required keys if not provided
        var keys = requiredKeys?.ToList() ?? (await _signatureProvider.GetAvailableKeysAsync(cancellationToken)).ToList();

        // Sign transaction
        var chainId = _configuration.ChainId ?? (await GetInfoAsync(cancellationToken)).ChainId;
        var signatures = await _signatureProvider.SignAsync(chainId, keys, packedTransaction, cancellationToken);

        return new SignedTransaction
        {
            Transaction = transaction,
            Signatures = signatures.ToList(),
            PackedTransaction = packedTransaction
        };
    }

    /// <summary>
    /// Creates, signs, and broadcasts a transaction
    /// </summary>
    /// <param name="transaction">Transaction to send</param>
    /// <param name="requiredKeys">Optional list of required keys</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction ID</returns>
    public async Task<string> SendTransactionAsync(
        Transaction transaction,
        IEnumerable<string>? requiredKeys = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var signedTransaction = await SignTransactionAsync(transaction, requiredKeys, cancellationToken);
        
        // TODO: Implement actual broadcast to blockchain
        throw new NotImplementedException("Transaction broadcast pending");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates an EOS account name
    /// </summary>
    /// <param name="accountName">Account name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidAccountName(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName) || accountName.Length > 12)
            return false;

        // EOS account names: 1-12 chars, a-z, 1-5, and .
        return accountName.All(c => (c >= 'a' && c <= 'z') || (c >= '1' && c <= '5') || c == '.');
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the client and releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Configuration for EOS client
/// </summary>
public sealed record EosConfiguration
{
    /// <summary>
    /// HTTP endpoint for the EOS node
    /// </summary>
    public required string HttpEndpoint { get; init; }

    /// <summary>
    /// Chain ID (optional - will be fetched if not provided)
    /// </summary>
    public string? ChainId { get; init; }

    /// <summary>
    /// HTTP request timeout
    /// </summary>
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of blocks behind head to use for TAPOS
    /// </summary>
    public uint BlocksBehind { get; init; } = 3;

    /// <summary>
    /// Transaction expiration time in seconds
    /// </summary>
    public uint ExpireSeconds { get; init; } = 30;
}
