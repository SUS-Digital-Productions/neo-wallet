# SUS.EOS.Sharp NuGet Package

## Package Information

- **Package ID**: `SUS.EOS.Sharp`
- **Version**: 1.0.0
- **Target Framework**: .NET 10
- **Location**: `SUS.EOS.Sharp/nupkg/SUS.EOS.Sharp.1.0.0.nupkg`

## Publishing to NuGet.org

### 1. Get NuGet API Key
1. Go to https://www.nuget.org
2. Sign in or create account
3. Go to API Keys
4. Create new API key with "Push" permissions

### 2. Push Package
```powershell
dotnet nuget push "c:\Users\pasag\Desktop\SUS Projects\neo-wallet\SUS.EOS.NeoWallet\SUS.EOS.Sharp\nupkg\SUS.EOS.Sharp.1.0.0.nupkg" --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Installing the Package

### From Local Folder
```powershell
# Add local package source
dotnet nuget add source "c:\Users\pasag\Desktop\SUS Projects\neo-wallet\SUS.EOS.NeoWallet\SUS.EOS.Sharp\nupkg" --name "Local SUS Packages"

# Install package
dotnet add package SUS.EOS.Sharp --version 1.0.0 --source "Local SUS Packages"
```

### From NuGet.org (After Publishing)
```powershell
dotnet add package SUS.EOS.Sharp
```

Or via Package Manager:
```
Install-Package SUS.EOS.Sharp
```

## Using the Library

### Basic Usage
```csharp
using SUS.EOS.Sharp;
using SUS.EOS.Sharp.Models;

// Configure client
var config = new EosConfiguration
{
    HttpEndpoint = "https://nodes.eos42.io",
    ChainId = "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
    ExpireSeconds = 30,
    BlocksBehind = 3
};

// Create client
using var client = new EosClient(config);

// Get chain info
var info = await client.GetInfoAsync();
Console.WriteLine($"Chain ID: {info.ChainId}");
Console.WriteLine($"Head Block: {info.HeadBlockNum}");

// Get account
var account = await client.GetAccountAsync("myaccount");
Console.WriteLine($"RAM: {account.RamUsage}/{account.RamQuota}");
```

### Working with Assets
```csharp
using SUS.EOS.Sharp.Models;

// Parse asset string
var asset = Asset.Parse("100.0000 EOS");
Console.WriteLine($"Amount: {asset.Amount}");      // 100.0000
Console.WriteLine($"Symbol: {asset.Symbol}");      // EOS
Console.WriteLine($"Precision: {asset.Precision}"); // 4

// Format asset
Console.WriteLine(asset);  // "100.0000 EOS"

// Try parse
if (Asset.TryParse("50.00 GAS", out var gasAsset))
{
    Console.WriteLine($"Parsed: {gasAsset}");
}
```

### Creating Transactions
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
                to = "recipient",
                quantity = "10.0000 EOS",
                memo = "Payment"
            }
        }
    }
};

// Sign and send (requires providers)
var txId = await client.SendTransactionAsync(transaction);
```

## Package Contents

The package includes:
- ✅ `EosClient` - Main blockchain client
- ✅ `EosConfiguration` - Configuration record
- ✅ All model types (Transaction, Account, Asset, ChainInfo)
- ✅ Provider interfaces (ISignatureProvider, IAbiSerializationProvider)
- ✅ Full XML documentation
- ✅ Symbol package (.snupkg) for debugging

## Version History

### 1.0.0 (2026-01-04)
- Initial release
- Modern .NET 10 implementation
- Core models and client structure
- Provider interfaces
- Asset parsing functionality

## Support

- **Repository**: https://github.com/susprojects/neo-wallet
- **Issues**: https://github.com/susprojects/neo-wallet/issues
- **Documentation**: See README.md in package

## License

MIT License - See LICENSE file for details
