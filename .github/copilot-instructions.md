# SUS.EOS.NeoWallet - AI Coding Instructions

## Project Overview
This repository contains the Neo Wallet application — a cross-platform wallet for Antelope (EOSIO) blockchains built with Tauri 2, React, and a backend layer that differs by platform:
- **Desktop**: .NET 10 sidecar process spawned by Tauri
- **Mobile**: Embedded Rust HTTP backend (axum) running inside the Tauri app

## Current Repository Scope
- **SUS.EOS.Sharp** - Antelope blockchain client, cryptography, serialization, and transaction helpers
- **SUS.EOS.EosioSigningRequest** - ESR protocol models and services
- **SUS.EOS.Sharp.Tests / TestAction** - test and example projects
- **desktop/app** - Tauri 2 + React frontend (desktop & mobile)
- **desktop/backend** - .NET 10 local backend (desktop sidecar)

## Architecture

### Desktop
```
React + Vite renderer
    ↓
Tauri desktop shell (tray icon, single-instance, deep links)
    ↓
Local .NET backend / sidecar (port 5199)
    ↓
SUS.EOS.EosioSigningRequest
    ↓
SUS.EOS.Sharp
```

### Mobile (Android / iOS)
```
React (bundled in Tauri Mobile webview)
    ↓
Tauri mobile shell (deep links)
    ↓
Embedded Rust HTTP backend (axum, port 5199)
    ↓
AES-256-CBC wallet crypto (byte-compatible with .NET)
```

## Coding Guidance

### Primary Direction
- Prefer changes that strengthen the reusable backend surface.
- Keep wallet secrets, signing, protocol handling, and chain access in .NET (desktop) or Rust (mobile).
- Treat React/Tauri as the UI shell, not the signing layer.
- The mobile Rust backend must maintain API compatibility with the .NET desktop backend.
- Wallet file encryption format (AES-256-CBC + PBKDF2-SHA256) must stay byte-compatible across platforms.

### Modern C# Features
- Use file-scoped namespaces.
- Keep nullable reference types enabled.
- Prefer records or immutable DTOs where appropriate.
- All async I/O should accept `CancellationToken cancellationToken = default`.
- Use `using` declarations and implement `IDisposable` where resources require it.

### Architecture Conventions
- Favor interface-first services.
- Keep transport and protocol boundaries explicit.
- Avoid introducing UI concerns into the remaining .NET libraries.
- If desktop-shell-facing behavior is needed, prefer documenting or defining a local API contract rather than reintroducing UI code.

### Error Handling
- Use explicit logging with `System.Diagnostics.Trace.WriteLine`.
- Log context-rich messages with a stable `[COMPONENT]` prefix.
- Throw coherent exceptions instead of swallowing errors.

### Documentation Expectations
- Keep docs aligned with the Tauri/React plus local .NET architecture.
- Remove or update documentation immediately when it references deleted MAUI behavior.

### What Not To Reintroduce
- Do not add new MAUI pages, Shell routes, or platform heads.
- Do not add UI logic to the remaining .NET libraries.
- Do not route private keys or signing through JavaScript unless explicitly asked for that tradeoff.

### Debug Logging Pattern
**Use System.Diagnostics.Trace for debug output:**
```csharp
System.Diagnostics.Trace.WriteLine("[COMPONENT] Action started");
System.Diagnostics.Trace.WriteLine($"[COMPONENT] Processing {itemCount} items");
System.Diagnostics.Trace.WriteLine($"[COMPONENT] Error occurred: {ex.Message}");
```

**Logging conventions:**
- Use `[COMPONENTNAME]` prefix in square brackets
- Use descriptive messages, include context
- Log entry/exit of important methods
- Log connection state changes
- Log full exception details for errors

### Model Organization
**Separate models by category in** `Services/Models/{Category}/`:
- `WalletData/` - Wallet, accounts, keys
- `AnchorCallback/` - Anchor protocol callbacks
- Place ESR models in dedicated `SUS.EOS.EosioSigningRequest.Models` library

**Model class conventions:**
```csharp
using System.Text.Json.Serialization;

namespace SUS.EOS.NeoWallet.Services.Models.WalletData;

/// <summary>
/// Wallet account entry
/// </summary>
public class WalletAccount
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "neowallet.v1.wallet";

    [JsonPropertyName("data")]
    public WalletAccountData? Data { get; set; }
}
```

## Key Files

- `SUS.EOS.NeoWallet/SUS.EOS.Sharp/SUS.EOS.Sharp.csproj` - Antelope blockchain client library
- `SUS.EOS.NeoWallet/SUS.EOS.EosioSigningRequest/SUS.EOS.EosioSigningRequest.csproj` - ESR protocol library
- `desktop/backend/Program.cs` - .NET local backend entry point
- `desktop/backend/ServiceRegistration.cs` - DI registration for backend services
- `desktop/app/src/App.tsx` - React router and page layout
- `desktop/app/src/api/client.ts` - Frontend API client for backend communication

## Adding New Features

### New React Pages
1. Create a new `.tsx` file in `desktop/app/src/pages/`
2. Add a route in `desktop/app/src/App.tsx`
3. Add a nav link in `desktop/app/src/Layout.tsx` if needed

### New Backend Endpoints
1. Create an endpoint class in `desktop/backend/Endpoints/`
2. Add DTOs in `desktop/backend/Dto/`
3. Map the endpoint in `desktop/backend/Program.cs`
4. Add corresponding API client function in `desktop/app/src/api/client.ts`

### Backend Services
- Services live in `desktop/backend/Services/`
- Register services in `desktop/backend/ServiceRegistration.cs`

## Build & Debug

### .NET Backend
```bash
cd desktop/backend
dotnet run
```
The backend starts on `http://localhost:5199` and prints `BACKEND_TOKEN=...` to stdout.

### React Frontend
```bash
cd desktop/app
npm install
npm run dev
```
The dev server starts on `http://localhost:1420`.

### Full Solution Build
```bash
cd SUS.EOS.NeoWallet
dotnet build SUS.EOS.NeoWallet.slnx
```

### Debug Logging
Use `System.Diagnostics.Trace.WriteLine` with `[COMPONENT]` prefix (see Debug Logging Pattern section).

## Current State
- **Architecture**: Tauri 2 + React 19 + Vite 6 + .NET 10 local backend sidecar
- **Frontend**: 7 pages (Dashboard, Send, Receive, Import, Unlock, Settings, ESR Approval)
- **Backend**: Minimal API with real blockchain wiring via SUS.EOS.Sharp
- **Wallet Storage**: AES-256-CBC encrypted wallet file at `%LocalAppData%/NeoWallet/wallet.json`
- **Auth**: Bearer token middleware with exemptions for health/create/unlock endpoints
- **SUS.EOS.Sharp Library**: Modern .NET 10 blockchain library based on eos-sharp with improvements
- Uses nullable reference types (`<Nullable>enable</Nullable>`)
- Implicit usings enabled across all projects

## SUS.EOS.Sharp Library

### Overview
Modern .NET 10 blockchain client library for Antelope (EOSIO) blockchains.

**Key Features:**
- ✅ Modern C# 13 with records, nullable reference types, file-scoped namespaces
- ✅ Full async/await with `CancellationToken` support throughout
- ✅ Strongly typed immutable models
- ✅ Interface-based design with `IDisposable` pattern
- ✅ ABI-based automatic binary serialization
- ✅ Secp256k1 cryptographic signing with BouncyCastle
- ✅ Support for all Antelope chains (WAX, EOS, Telos, etc.)

### Architecture
- **Location**: `SUS.EOS.Sharp/` project
- **Target**: .NET 10.0 class library
- **Design**: Production-ready, used in real blockchain transactions

### Key Classes
- `IAntelopeBlockchainClient` / `AntelopeHttpClient` - Blockchain client interface and HTTP implementation
- `IAntelopeTransactionService` / `AntelopeTransactionService` - Transaction building and signing
- `IBlockchainOperationsService` / `BlockchainOperationsService` - High-level operations
- `EosioKey` - Key management (WIF, PVT_K1_, hex formats)
- `AbiSerializer` - ABI-based binary serialization
- `EosioSerializer` - Transaction binary encoding

### Usage Pattern
```csharp
// Create blockchain client
using var client = new AntelopeHttpClient("https://wax.greymass.com");

// Get chain info
var chainInfo = await client.GetInfoAsync(cancellationToken);

// Build and sign transaction
var txService = new AntelopeTransactionService(client);
var signatureProvider = new EosioSignatureProvider(privateKeyWif);

var result = await txService.BuildAndSignTransactionAsync(
    actions: new[]
    {
        new
        {
            account = "eosio.token",
            name = "transfer",
            authorization = new[] { new { actor = "myaccount", permission = "active" } },
            data = new { from = "myaccount", to = "receiver", quantity = "1.00000000 WAX", memo = "test" }
        }
    },
    signatureProvider: signatureProvider,
    cancellationToken: cancellationToken
);

// Broadcast
var pushResult = await client.PushTransactionAsync(result, cancellationToken);
```

### ABI Serialization
The library automatically handles ABI serialization:
```csharp
// Define action data as plain object - ABI handles serialization
var actionData = new 
{ 
    from = "sender", 
    to = "receiver", 
    quantity = "10.0000 TOKEN",
    memo = "payment"
};

// ABI fetched automatically, binary serialization handled internally
```

### Asset Parsing
```csharp
var asset = Asset.Parse("100.0000 EOS");
Console.WriteLine(asset.Amount);    // 100.0000
Console.WriteLine(asset.Symbol);    // EOS
Console.WriteLine(asset.Precision); // 4
Console.WriteLine(asset);           // "100.0000 EOS"
```

## SUS.EOS.EosioSigningRequest Library

### Overview
Dedicated library for ESR (EOSIO Signing Request) protocol v3 implementation.

**Key Features:**
- ✅ ESR URI parsing and encoding
- ✅ Request signing with blockchain integration
- ✅ Anchor Link compatible WebSocket session management
- ✅ Identity request support
- ✅ Callback handling

### Architecture
- **Location**: `SUS.EOS.EosioSigningRequest/` project
- **Target**: .NET 10.0 class library
- **Dependencies**: References `SUS.EOS.Sharp` for crypto and blockchain models

### Namespace Structure
```
SUS.EOS.EosioSigningRequest
├── Esr (core class)
├── Models/
│   ├── EsrRequestPayload
│   ├── EsrFlags
│   ├── EsrCallbackResponse
│   ├── EsrSession
│   ├── EsrSessionStatus
│   ├── EsrSigningRequestEventArgs
│   ├── EsrSessionStatusEventArgs
│   ├── EsrCallbackPayload
│   └── EsrMessageEnvelope
└── Services/
    ├── IEsrService / EsrService
    └── IEsrSessionManager (impl in NeoWallet due to MAUI dependency)
```

### Service Interfaces

**IEsrService** - ESR parsing and signing:
```csharp
public interface IEsrService
{
    Task<Esr> ParseRequestAsync(string uri);
    Task<EsrCallbackResponse> SignRequestAsync(
        Esr request, 
        string privateKeyWif, 
        object? blockchainClient = null,
        bool broadcast = false,
        CancellationToken cancellationToken = default
    );
    Task<EsrCallbackResponse> SignAndBroadcastAsync(...);
    Task<bool> SendCallbackAsync(Esr request, EsrCallbackResponse response);
}
```

**IEsrSessionManager** - WebSocket session management (Anchor Link compatible):
```csharp
public interface IEsrSessionManager
{
    event EventHandler<EsrSigningRequestEventArgs>? SigningRequestReceived;
    event EventHandler<EsrSessionStatusEventArgs>? StatusChanged;
    
    EsrSessionStatus Status { get; }
    string LinkId { get; }
    string? RequestPublicKey { get; }
    IReadOnlyList<EsrSession> Sessions { get; }
    
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendCallbackAsync(EsrCallbackPayload callback);
    Task AddSessionAsync(EsrSession session);
    Task<bool> RemoveSessionAsync(string actor, string permission, string chainId);
    Task ClearSessionsAsync();
}
```

### Usage Pattern
```csharp
// Parse ESR
var esrService = new EsrService();
var request = await esrService.ParseRequestAsync("esr://...");

// Sign with blockchain client
var response = await esrService.SignRequestAsync(
    request,
    privateKeyWif: "5K...",
    blockchainClient: blockchainClient,
    broadcast: true
);

// Send callback
await esrService.SendCallbackAsync(request, response);
```

### Namespace Conflict Resolution
When using ESR library, use alias to avoid namespace/class name conflict:
```csharp
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;
using EsrRequest = SUS.EOS.EosioSigningRequest.Esr;  // Alias to avoid conflict

// Now use EsrRequest instead of Esr
private async Task ProcessRequest(EsrRequest request)
{
    // ...
}
```

## Services Layer

### Backend Service Organization
Services live in `desktop/backend/Services/` and are registered in `desktop/backend/ServiceRegistration.cs`.

### Core Services

**IWalletStorageService** - Encrypted wallet file on disk:
- AES-256-CBC with PBKDF2 key derivation
- Create / unlock / lock / save wallet file
- Stored at `%LocalAppData%/NeoWallet/wallet.json`

**IWalletStateService** - Wallet state management:
- Lock/unlock, active account/network
- Import/remove accounts with key validation via `EosioKey`
- Retrieve private keys for signing

**IChainClientFactory** - Blockchain client creation:
- `CreateRpcClient(chainId)` → `AntelopeHttpClient`
- `CreateLightApiClient(chainId)` → `LightApiClient`

**IAntelopeTransactionService** - Transaction building and signing

**IEsrService** - ESR parsing, signing, and callbacks

### Service Implementation Pattern
```csharp
namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Service description
/// </summary>
public class MyService : IMyService, IDisposable
{
    private readonly IDependency _dependency;
    private bool _disposed;

    public MyService(IDependency dependency)
    {
        _dependency = dependency;
    }

    public async Task<Result> PerformOperationAsync(
        string parameter,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);

        try
        {
            var result = await _dependency.ProcessAsync(parameter, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[MYSERVICE] Error: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Cleanup resources
            _disposed = true;
        }
    }
}
```

## Navigation Structure

### Sidebar Navigation (Layout.tsx)
```
Dashboard  → /
Send       → /send
Receive    → /receive
Import     → /import
Settings   → /settings
ESR        → /esr
```

### Routes (App.tsx)
- All main routes wrapped in `<Layout />` with sidebar
- `/unlock` is a standalone page (no sidebar)

## Security Best Practices

### Never Hardcode Secrets
 DON'T: `const string PrivateKey = "5K...";`
 DO: Use SecureStorage or environment variables

### Input Validation
Always validate input parameters:
```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(account);
ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
if (!IsValidFormat(account))
    throw new ArgumentException("Invalid format", nameof(account));
```
