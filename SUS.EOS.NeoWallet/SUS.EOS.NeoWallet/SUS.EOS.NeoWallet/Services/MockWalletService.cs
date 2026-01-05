using SUS.EOS.Sharp.Models;

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Mock wallet service with fake data for demonstration
/// </summary>
public sealed class MockWalletService : IWalletService
{
    private readonly List<Asset> _assets;
    private readonly List<TransactionHistory> _transactions;
    private readonly string _walletAddress;

    public MockWalletService()
    {
        _walletAddress = "neowallet.gm";
        
        // Initialize mock assets
        _assets = new List<Asset>
        {
            new Asset { Amount = 1250.5000m, Precision = 4, Symbol = "NEO" },
            new Asset { Amount = 450.25m, Precision = 2, Symbol = "GAS" },
            new Asset { Amount = 10000.0000m, Precision = 4, Symbol = "EOS" },
            new Asset { Amount = 5000.00m, Precision = 2, Symbol = "USDT" }
        };

        // Initialize mock transaction history
        _transactions = new List<TransactionHistory>
        {
            new TransactionHistory
            {
                TransactionId = "0x1a2b3c4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890",
                Timestamp = DateTime.Now.AddHours(-2),
                From = "alice.gm",
                To = _walletAddress,
                Amount = new Asset { Amount = 100.0000m, Precision = 4, Symbol = "NEO" },
                Memo = "Payment for services",
                Status = TransactionStatus.Confirmed
            },
            new TransactionHistory
            {
                TransactionId = "0x2b3c4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890ab",
                Timestamp = DateTime.Now.AddHours(-5),
                From = _walletAddress,
                To = "bob.gm",
                Amount = new Asset { Amount = 50.00m, Precision = 2, Symbol = "GAS" },
                Memo = "Gas refund",
                Status = TransactionStatus.Confirmed
            },
            new TransactionHistory
            {
                TransactionId = "0x3c4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890abcd",
                Timestamp = DateTime.Now.AddDays(-1),
                From = "charlie.gm",
                To = _walletAddress,
                Amount = new Asset { Amount = 1000.0000m, Precision = 4, Symbol = "EOS" },
                Memo = "Token swap",
                Status = TransactionStatus.Confirmed
            },
            new TransactionHistory
            {
                TransactionId = "0x4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
                Timestamp = DateTime.Now.AddDays(-2),
                From = _walletAddress,
                To = "dave.gm",
                Amount = new Asset { Amount = 250.0000m, Precision = 4, Symbol = "NEO" },
                Memo = "Investment",
                Status = TransactionStatus.Confirmed
            },
            new TransactionHistory
            {
                TransactionId = "0x5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890abcdef12",
                Timestamp = DateTime.Now.AddDays(-3),
                From = "exchange.gm",
                To = _walletAddress,
                Amount = new Asset { Amount = 500.00m, Precision = 2, Symbol = "USDT" },
                Memo = "Withdrawal from exchange",
                Status = TransactionStatus.Confirmed
            },
            new TransactionHistory
            {
                TransactionId = "0x6f7890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234",
                Timestamp = DateTime.Now.AddMinutes(-15),
                From = _walletAddress,
                To = "eve.gm",
                Amount = new Asset { Amount = 25.0000m, Precision = 4, Symbol = "NEO" },
                Memo = "Test transaction",
                Status = TransactionStatus.Pending
            }
        };
    }

    public Task<Asset> GetBalanceAsync()
    {
        // Return the main NEO balance
        return Task.FromResult(_assets[0]);
    }

    public Task<IReadOnlyList<Asset>> GetAssetsAsync()
    {
        return Task.FromResult<IReadOnlyList<Asset>>(_assets);
    }

    public Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int count = 10)
    {
        var history = _transactions
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<TransactionHistory>>(history);
    }

    public Task<string> GetAddressAsync()
    {
        return Task.FromResult(_walletAddress);
    }

    public async Task<string> SendAsync(string toAddress, Asset amount, string memo)
    {
        // Simulate network delay
        await Task.Delay(1000);

        // Generate mock transaction ID
        var txId = $"0x{Guid.NewGuid():N}";

        // Add to transaction history
        _transactions.Add(new TransactionHistory
        {
            TransactionId = txId,
            Timestamp = DateTime.Now,
            From = _walletAddress,
            To = toAddress,
            Amount = amount,
            Memo = memo,
            Status = TransactionStatus.Pending
        });

        // Update balance (subtract sent amount)
        var asset = _assets.FirstOrDefault(a => a.Symbol == amount.Symbol);
        if (asset != null)
        {
            var index = _assets.IndexOf(asset);
            _assets[index] = asset with { Amount = asset.Amount - amount.Amount };
        }

        return txId;
    }
}
