using SUS.EOS.Sharp;
using SUS.EOS.Sharp.Models;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Mock wallet service for demonstration purposes
/// </summary>
public interface IWalletService
{
    /// <summary>
    /// Gets the current wallet balance
    /// </summary>
    Task<Asset> GetBalanceAsync();

    /// <summary>
    /// Gets all assets in the wallet
    /// </summary>
    Task<IReadOnlyList<Asset>> GetAssetsAsync();

    /// <summary>
    /// Gets recent transactions
    /// </summary>
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int count = 10);

    /// <summary>
    /// Gets the wallet address
    /// </summary>
    Task<string> GetAddressAsync();

    /// <summary>
    /// Sends assets to another address
    /// </summary>
    Task<string> SendAsync(string toAddress, Asset amount, string memo);
}

/// <summary>
/// Transaction history entry
/// </summary>
public sealed record TransactionHistory
{
    public required string TransactionId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required Asset Amount { get; init; }
    public required string Memo { get; init; }
    public required TransactionStatus Status { get; init; }
}

/// <summary>
/// Transaction status
/// </summary>
public enum TransactionStatus
{
    Pending,
    Confirmed,
    Failed
}
