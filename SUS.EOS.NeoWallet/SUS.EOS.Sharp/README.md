# SUS.EOS.Sharp

A modern .NET 10 library for interacting with EOS-based blockchains, inspired by [eos-sharp](https://github.com/GetScatter/eos-sharp) but rebuilt with modern .NET best practices.

## Features

- **Modern .NET 10**: Built with the latest .NET features including records, nullable reference types, and C# 13
- **Async/Await Pattern**: Full async support with `CancellationToken` for all operations
- **Strongly Typed Models**: Immutable record types for all blockchain entities
- **Interface-Based Design**: Clean separation of concerns with provider interfaces
- **Resource Management**: Proper `IDisposable` implementation for the client
- **Best Practices**: Follows Microsoft's .NET coding guidelines and patterns

## Architecture

### Core Classes

- **`EosClient`**: Main client for blockchain interactions
- **`EosConfiguration`**: Type-safe configuration with sensible defaults
- **`Transaction`**, **`SignedTransaction`**: Transaction models
- **`Account`**: Account information and resources
- **`Asset`**: Type-safe asset representation with parsing
- **`ChainInfo`**: Blockchain metadata

### Provider Interfaces

- **`ISignatureProvider`**: Interface for signing transactions (implement for custom signing)
- **`IAbiSerializationProvider`**: Interface for ABI serialization/deserialization

## Usage

### Basic Setup

```csharp
using SUS.EOS.Sharp;
using SUS.EOS.Sharp.Models;

var config = new EosConfiguration
{
    HttpEndpoint = "https://nodes.eos42.io",
    ChainId = "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
    ExpireSeconds = 30,
    BlocksBehind = 3
};

using var client = new EosClient(config);
```

### Get Chain Information

```csharp
var info = await client.GetInfoAsync();
Console.WriteLine($"Head Block: {info.HeadBlockNum}");
Console.WriteLine($"Chain ID: {info.ChainId}");
```

### Get Account Information

```csharp
var account = await client.GetAccountAsync("myaccount");
Console.WriteLine($"RAM: {account.RamUsage}/{account.RamQuota} bytes");
Console.WriteLine($"CPU: {account.CpuLimit.Used}/{account.CpuLimit.Max} μs");
```

### Parse Assets

```csharp
var asset = Asset.Parse("100.0000 EOS");
Console.WriteLine($"Amount: {asset.Amount}");
Console.WriteLine($"Symbol: {asset.Symbol}");
Console.WriteLine($"Precision: {asset.Precision}");
Console.WriteLine($"Formatted: {asset}"); // "100.0000 EOS"
```

### Sign and Send Transactions

```csharp
var transaction = new Transaction
{
    Expiration = DateTime.UtcNow.AddSeconds(30),
    RefBlockNum = 12345,
    RefBlockPrefix = 67890,
    Actions = new[]
    {
        new Models.Action
        {
            Account = "eosio.token",
            Name = "transfer",
            Authorization = new[]
            {
                new PermissionLevel
                {
                    Actor = "myaccount",
                    Permission = "active"
                }
            },
            Data = new
            {
                from = "myaccount",
                to = "otheraccount",
                quantity = "1.0000 EOS",
                memo = "Hello EOS!"
            }
        }
    }
};

// Sign (requires signature provider)
var signedTx = await client.SignTransactionAsync(transaction);

// Send (signs and broadcasts)
var txId = await client.SendTransactionAsync(transaction);
Console.WriteLine($"Transaction ID: {txId}");
```

## Improvements Over eos-sharp

1. **Modern C# Features**: Records, nullable reference types, file-scoped namespaces
2. **Better Error Handling**: `ArgumentException.ThrowIfNullOrWhiteSpace`, `ObjectDisposedException.ThrowIf`
3. **Immutability**: All models are immutable records with `init` properties
4. **Async Best Practices**: Proper `CancellationToken` support throughout
5. **Resource Safety**: `IDisposable` pattern with proper disposal
6. **Type Safety**: Strongly typed models instead of dynamic objects
7. **Documentation**: XML documentation comments on all public APIs
8. **Validation**: Input validation with descriptive exceptions

## Differences from eos-sharp

| Feature | eos-sharp | SUS.EOS.Sharp |
|---------|-----------|---------------|
| .NET Version | .NET Framework / .NET Core 3.1 | .NET 10 |
| Records | Classes | Records |
| Nullability | Disabled | Enabled |
| Async Pattern | Async/Await | Async/Await with CancellationToken |
| Configuration | Mutable class | Immutable record |
| Assets | String parsing | Strongly-typed Asset record |
| Disposal | Manual | IDisposable pattern |
| Documentation | Partial | Complete XML docs |

## Integration with Neo Wallet

This library is designed to be integrated with the SUS.EOS.NeoWallet MAUI application. See the wallet's `MockWalletService` for example usage patterns.

## Status

⚠️ **Development Status**: Core structure complete, API integration pending.

Completed:
- ✅ Modern .NET 10 project structure
- ✅ Core model types (Transaction, Account, Asset, ChainInfo)
- ✅ Provider interfaces (ISignatureProvider, IAbiSerializationProvider)
- ✅ EosClient with basic method signatures
- ✅ Asset parsing and formatting

Pending:
- ⏳ HTTP API implementation
- ⏳ ABI serialization implementation
- ⏳ Default signature provider
- ⏳ Transaction signing implementation
- ⏳ Error handling and exceptions
- ⏳ Unit tests

## License

MIT License - See LICENSE file for details
