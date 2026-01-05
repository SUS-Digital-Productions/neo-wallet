# Neo Wallet - Integration Guide

This guide demonstrates how all the wallet components work together.

## Complete Flow Example

### 1. Initialize Wallet and Create Account

```csharp
// Inject services
var walletAccountService = serviceProvider.GetRequiredService<IWalletAccountService>();
var networkService = serviceProvider.GetRequiredService<INetworkService>();
var anchorCallbackService = serviceProvider.GetRequiredService<IAnchorCallbackService>();

// Initialize predefined networks
await networkService.InitializePredefinedNetworksAsync();

// Set default network (WAX)
await networkService.SetDefaultNetworkAsync("1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4");

// Create new account with random mnemonic
var (account, mnemonic) = await walletAccountService.GenerateNewAccountAsync(
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
    accountName: "myaccount",
    authority: "active",
    password: "MySecurePassword123!"
);

// Show mnemonic to user for backup
Console.WriteLine($"IMPORTANT: Save this mnemonic phrase:");
Console.WriteLine(mnemonic);
```

### 2. Import Existing Account

```csharp
// From mnemonic
var importedAccount = await walletAccountService.ImportAccountFromMnemonicAsync(
    mnemonic: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
    accountName: "existingacct",
    authority: "active",
    password: "MySecurePassword123!",
    accountIndex: 0
);

// From private key
var privateKeyAccount = await walletAccountService.ImportAccountAsync(
    privateKey: "YOUR_PRIVATE_KEY_HERE",
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
    accountName: "myaccount",
    authority: "active",
    password: "MySecurePassword123!"
);
```

### 3. Send Transaction Using Wallet

```csharp
// Get blockchain client
var client = serviceProvider.GetRequiredService<IAntelopeBlockchainClient>();

// Create signature provider from wallet
var signatureProvider = WalletSignatureProvider.CreateForCurrentAccount(
    walletAccountService,
    storageService,
    password: "MySecurePassword123!"
);

// Create operations service
var operationsService = signatureProvider.CreateOperationsService(client);

// Send tokens
var result = await operationsService.TransferAsync(
    from: "myaccount",
    to: "receiver123",
    quantity: "10.0000 WAX",
    memo: "Payment for services",
    cancellationToken: CancellationToken.None
);

Console.WriteLine($"Transaction ID: {result.TransactionId}");
```

### 4. Handle ESR Signing Request (dApp Integration)

```csharp
// Receive ESR from dApp (via deep link or QR code)
var esrUri = "esr://gmNgZGBY1mTC_MoglIGBIVzX5uxZRqAQGMBoExgDAjRi4fwAVz93ICUckpGYl12skJZfpFCSkaqQllmcmpOZl66QmZeukJZYpJCbmKOQmJuYm5iXrpCSmZ6qkJSTnwIAMRQhCQ";

// Handle signing request
var result = await anchorCallbackService.HandleSigningRequestAsync(
    esrUri,
    password: "MySecurePassword123!" // Only needed if wallet is locked
);

if (result.Success)
{
    Console.WriteLine($"Transaction signed by: {result.Account}@{result.Permission}");
    Console.WriteLine($"Transaction ID: {result.Response?.TransactionId}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

### 5. Handle Anchor Protocol Deep Links

```csharp
// Initialize protocol handler
AnchorProtocolHandler.Initialize(anchorCallbackService);

// Handle various deep link protocols
await AnchorProtocolHandler.HandleUriAsync("esr://...");  // ESR protocol
await AnchorProtocolHandler.HandleUriAsync("anchor://sign?esr=...");  // Sign transaction
await AnchorProtocolHandler.HandleUriAsync("anchor://identity?callback=https://dapp.com/cb");  // Provide identity
```

### 6. Multi-Chain Operations

```csharp
// Get available networks
var networks = await networkService.GetNetworksAsync();
foreach (var network in networks)
{
    Console.WriteLine($"{network.Name} ({network.ChainId}): {network.HttpEndpoint}");
}

// Switch to different network
var eosNetwork = networks.FirstOrDefault(n => n.ChainId.StartsWith("aca376f2"));
if (eosNetwork != null)
{
    await networkService.SetDefaultNetworkAsync(eosNetwork.ChainId);
    
    // Update blockchain client endpoint
    var eosClient = new AntelopeHttpClient(eosNetwork.HttpEndpoint);
    
    // Create new operations service for EOS
    var eosOperations = signatureProvider.CreateOperationsService(eosClient);
    
    // Now transactions will go to EOS mainnet
}
```

### 7. Account Management

```csharp
// List all accounts
var accounts = await walletAccountService.GetAccountsAsync();
foreach (var acc in accounts)
{
    Console.WriteLine($"{acc.Data.Account}@{acc.Data.Authority} on {acc.Data.ChainId}");
}

// Export account key (for backup)
var exportedKey = await walletAccountService.ExportAccountKeyAsync(
    account: "myaccount",
    authority: "active",
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
    password: "MySecurePassword123!"
);
Console.WriteLine($"Private Key: {exportedKey}");

// Remove account
await walletAccountService.RemoveAccountAsync(
    account: "myaccount",
    authority: "active",
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4"
);
```

### 8. Custom Callback Handler

```csharp
// Register custom handler for transaction signing
anchorCallbackService.RegisterCallbackHandler(
    chainId: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
    handler: async (request) =>
    {
        // Custom logic when transaction is signed
        Console.WriteLine($"Transaction signed for chain: {request.ChainId}");
        
        // Could show notification, log to analytics, etc.
        return true;
    }
);
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         UI Layer                             │
│   (Pages: Dashboard, Send, Receive, Import, etc.)           │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                   Service Layer                              │
├──────────────────────────────────────────────────────────────┤
│  WalletAccountService  │  NetworkService  │  AnchorCallback  │
│  WalletStorageService  │  Cryptography    │  ESR Service     │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│              Blockchain Layer (SUS.EOS.Sharp)                │
├──────────────────────────────────────────────────────────────┤
│  AntelopeBlockchainClient  │  AntelopeTransactionService     │
│  WalletSignatureProvider   │  BlockchainOperationsService    │
│  EosioSignatureProvider    │  EOSIO Signing Request (ESR)    │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                  Cryptography Layer                          │
├──────────────────────────────────────────────────────────────┤
│  NBitcoin (BIP39/BIP44)  │  ECDSA (secp256k1)               │
│  AES-256-CBC Encryption  │  PBKDF2 Key Derivation           │
└──────────────────────────────────────────────────────────────┘
```

## Component Relationships

### WalletSignatureProvider - The Bridge

The `WalletSignatureProvider` is the crucial bridge that connects:
- **High-level**: `WalletAccountService` (manages encrypted keys in wallet.json)
- **Low-level**: `EosioSignatureProvider` (signs raw transaction bytes)

This allows seamless transaction signing:
```csharp
User requests transaction
    ↓
WalletAccountService retrieves encrypted private key
    ↓
CryptographyService decrypts key with password
    ↓
WalletSignatureProvider creates EosioSignatureProvider
    ↓
EosioSignatureProvider signs transaction bytes
    ↓
Signed transaction broadcast to blockchain
```

### Anchor Callback Service - dApp Integration

The `AnchorCallbackService` enables this wallet to act as an Anchor replacement:

```
dApp generates ESR URI → User clicks/scans → Wallet opens
                                                    ↓
                                         Parse ESR request
                                                    ↓
                                       Show transaction details
                                                    ↓
                                         User approves/rejects
                                                    ↓
                                    Sign with WalletSignatureProvider
                                                    ↓
                                    Send callback to dApp
                                                    ↓
                                          dApp receives signed TX
```

## Security Considerations

1. **Password Protection**: All private keys encrypted with AES-256-CBC
2. **PBKDF2 Key Derivation**: 4500 iterations (Anchor-compatible)
3. **Memory Security**: Keys cleared after use
4. **Secure Storage**: Wallet data stored in isolated application data folder
5. **Backup Encryption**: Export files use same encryption as wallet.json

## Testing Checklist

- [ ] Create new wallet with mnemonic
- [ ] Import wallet from mnemonic
- [ ] Import wallet from private key
- [ ] Send transaction on WAX mainnet
- [ ] Send transaction on EOS mainnet
- [ ] Receive ESR from dApp
- [ ] Sign ESR transaction
- [ ] Handle anchor:// deep links
- [ ] Export private key
- [ ] Backup wallet.json
- [ ] Restore from backup
- [ ] Multi-account management
- [ ] Network switching

## Next Steps for Production

1. **UI Integration**: Connect services to XAML pages
2. **Biometric Auth**: Add fingerprint/face unlock
3. **Hardware Wallet**: Integrate Ledger/Trezor support
4. **QR Code**: Implement scanning for ESR/addresses
5. **Push Notifications**: Transaction confirmations
6. **Price Feeds**: Real-time token prices
7. **Transaction History**: Parse and display history
8. **NFT Support**: Display and transfer NFTs
9. **Staking**: Resource management UI
10. **Multi-sig**: Support multi-signature accounts
