# SUS.EOS.NeoWallet - AI Coding Instructions

## Project Overview
This is a cross-platform Neo blockchain wallet built with .NET MAUI targeting .NET 10.0. The solution follows a single-project architecture pattern with platform-specific head projects inspired by the Anchor wallet (greymass/anchor).

**Key Features:**
- Multi-blockchain support (WAX, EOS, Telos, and other Antelope chains)
- ESR (EOSIO Signing Request) protocol for dApp integration
- Anchor Link compatible WebSocket listener for real-time signing requests
- Secure wallet management with encrypted storage
- Modern async/await architecture throughout

## Architecture Pattern

### Project Structure
- **SUS.EOS.NeoWallet** - Shared core project containing all UI, business logic, and cross-platform code
- **SUS.EOS.NeoWallet.Droid** - Android platform head (targets `net10.0-android`, min SDK 21)
- **SUS.EOS.NeoWallet.iOS** - iOS platform head
- **SUS.EOS.NeoWallet.Mac** - macOS platform head
- **SUS.EOS.NeoWallet.WinUI** - Windows platform head (targets `net10.0-windows10.0.19041.0`)
- **SUS.EOS.Sharp** - Antelope blockchain client library (.NET 10.0)
- **SUS.EOS.EosioSigningRequest** - Dedicated ESR protocol library (.NET 10.0)

### Dependency Chain
```
SUS.EOS.Sharp (blockchain client, crypto, models)
    ↓
SUS.EOS.EosioSigningRequest (ESR protocol, parsing, signing)
    ↓
SUS.EOS.NeoWallet (MAUI wallet app, UI, storage)
```

### Single-Project MAUI Pattern
All platform heads use the `UseSharedMauiApp()` extension method defined in [MauiProgramExtensions.cs](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/MauiProgramExtensions.cs). Each platform's `MauiProgram.cs` simply calls:
```csharp
builder.UseSharedMauiApp();
```
This centralizes configuration (fonts, logging, etc.) in the shared project.

### Navigation
Uses Shell-based navigation defined in [AppShell.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/AppShell.xaml) with `FlyoutBehavior="Disabled"` for single-page flows. Navigation uses Shell routing with `Shell.Current.GoToAsync()`:
- Absolute routes: `//PageRoute` (e.g., `//DashboardPage`)
- Relative back: `..` to go back one level

## Page Structure and Organization

### Initialization Flow
1. [InitializePage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/InitializePage.xaml) - Main entry point with options to create/import/recover wallet
2. [CreateAccountPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/CreateAccountPage.xaml) - New wallet creation with password setup and validation
3. [WalletSetupPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/WalletSetupPage.xaml) - Multi-step setup flow showing seed phrase
4. [ImportWalletPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/ImportWalletPage.xaml) - Import via recovery phrase, private key, or file
5. [RecoverAccountPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/RecoverAccountPage.xaml) - Recover wallet from backup
6. [EnterPasswordPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/EnterPasswordPage.xaml) - Password entry for unlocking wallet

### Main Wallet Pages
1. [DashboardPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/DashboardPage.xaml) - Main overview with balance, assets, and recent transactions
2. [SendPage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/SendPage.xaml) - Send NEO/GAS with fee selection
3. [ReceivePage.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/Pages/ReceivePage.xaml) - Display QR code and address for receiving assets

## Code Conventions and Best Practices

### Modern C# Features (Use These!)
- ✅ **File-scoped namespaces**: `namespace SUS.EOS.NeoWallet.Pages;` (no braces)
- ✅ **Nullable reference types**: Enabled throughout (`<Nullable>enable</Nullable>`)
- ✅ **Records for immutable data**: Use `record` for DTOs and configuration objects
- ✅ **Init-only properties**: Use `init` instead of `set` for immutable properties
- ✅ **CancellationToken support**: All async methods should accept `CancellationToken cancellationToken = default`
- ✅ **IDisposable pattern**: Implement for resources (WebSockets, HTTP clients, etc.)
- ✅ **using declarations**: Prefer `using var client = ...` over `using (var client = ...) { }`

### Namespaces
- Root namespace: `SUS.EOS.NeoWallet`
- Pages: `namespace SUS.EOS.NeoWallet.Pages;`
- Services: `namespace SUS.EOS.NeoWallet.Services;`
- Models: `namespace SUS.EOS.NeoWallet.Services.Models.{Category};`
- Sharp library: `namespace SUS.EOS.Sharp.{Category};`
- ESR library: `namespace SUS.EOS.EosioSigningRequest.{Models|Services};`

### Service Layer Architecture
**All services follow interface-first design:**

1. **Create interface in** `Services/Interfaces/I{ServiceName}.cs`
2. **Implement in** `Services/{ServiceName}.cs`
3. **Register in** `MauiProgramExtensions.cs`

**Service Registration Pattern:**
```csharp
// Singleton for stateful services (storage, context, session managers)
builder.Services.AddSingleton<IWalletStorageService, WalletStorageService>();
builder.Services.AddSingleton<IWalletContextService, WalletContextService>();
builder.Services.AddSingleton<IEsrSessionManager, EsrSessionManager>();

// Transient for stateless operations
builder.Services.AddTransient<IAntelopeTransactionService, AntelopeTransactionService>();

// HttpClient registration
builder.Services.AddHttpClient();
```

### Dependency Injection in Pages
**ALWAYS use constructor injection:**
```csharp
public partial class MainPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly IAntelopeBlockchainClient _blockchainClient;

    public MainPage(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        IAntelopeBlockchainClient blockchainClient
    )
    {
        InitializeComponent();
        _accountService = accountService;
        _storageService = storageService;
        _blockchainClient = blockchainClient;
    }
}
```

**Register pages as transient in MauiProgramExtensions.cs:**
```csharp
builder.Services.AddTransient<MainPage>();
builder.Services.AddTransient<DashboardPage>();
```

### Async/Await Best Practices
✅ **DO:**
- Use `async`/`await` for all I/O operations
- Always provide `CancellationToken` parameter
- Return `Task` or `Task<T>`, never `async void` (except event handlers)
- Use `ConfigureAwait(false)` for library code (not in UI code)

```csharp
public async Task<WalletAccount> AddAccountAsync(
    string account,
    string authority,
    string chainId,
    string privateKey,
    string password,
    WalletMode mode = WalletMode.Hot
)
{
    var wallet = await _storageService.LoadWalletAsync();
    // ... implementation
}
```

❌ **DON'T:**
- Use `.Result` or `.Wait()` (causes deadlocks)
- Use `async void` except for event handlers
- Forget to pass `CancellationToken` through the call chain

### Error Handling
**Use try-catch with specific logging:**
```csharp
try
{
    var result = await _service.PerformOperationAsync();
    return result;
}
catch (HttpRequestException ex)
{
    System.Diagnostics.Trace.WriteLine($"[SERVICE] Network error: {ex.Message}");
    throw new InvalidOperationException("Network request failed", ex);
}
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"[SERVICE] Unexpected error: {ex.Message}");
    System.Diagnostics.Trace.WriteLine($"[SERVICE] Stack trace: {ex.StackTrace}");
    throw;
}
```

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

### XAML Files
- All XAML files use the MAUI 2021 schema: `http://schemas.microsoft.com/dotnet/2021/maui`
- Pages stored in `Pages/` directory with `.xaml` and `.xaml.cs` code-behind
- Use `x:Name` for controls that need code-behind access
- WinUI project sets `EnableDefaultMauiItems=false` to process XAML as WinUI XAML

### UI Patterns
- Pages use `ContentPage` with `ScrollView` > `VerticalStackLayout` for scrollable content
- Forms use `Frame` containers with rounded corners (`CornerRadius="12"`) and shadows
- Color references use `{DynamicResource Primary}`, `{DynamicResource Gray600}`, etc.
- Validation messages use named `Label` controls with `IsVisible` binding (e.g., `ErrorLabel`)
- Event handlers follow pattern: `OnActionClicked` (e.g., `OnLoginClicked`, `OnSendClicked`)
- Password fields use `Grid` with `Entry` and toggle visibility `Button` (eye icon: 👁)

### UI Components Patterns
**Card-Style Frames:**
```xml
<Frame BorderColor="{DynamicResource Gray200}" CornerRadius="12" Padding="20" HasShadow="True">
  <VerticalStackLayout Spacing="16">
    <!-- Content -->
  </VerticalStackLayout>
</Frame>
```

**Password Strength Indicators:**
Use 4 `Frame` elements with dynamic `BackgroundColor` based on strength level (see CreateAccountPage.xaml)

**Quick Action Buttons:**
Circular buttons with emoji icons in grid layout (see DashboardPage.xaml)

**Asset List Items:**
Grid with icon, name/value, and balance (see DashboardPage.xaml assets section)

## Key Files

- [MauiProgramExtensions.cs](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/MauiProgramExtensions.cs) - Shared app configuration entry point
- [AppShell.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/AppShell.xaml) - Shell navigation definition with all page routes
- [SUS.EOS.NeoWallet.csproj](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet.csproj) - Core project with `SingleProject=true`

## Adding New Features

### New Pages
1. Create `.xaml` and `.xaml.cs` in `Pages/` directory
2. Add route in [AppShell.xaml](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/AppShell.xaml):
   ```xml
   <ShellContent
     Title="Your Page"
     ContentTemplate="{DataTemplate local:Pages.YourPage}"
     Route="YourPage"
   />
   ```
3. Add to `.csproj`:
   ```xml
   <MauiXaml Update="Pages\YourPage.xaml">
     <Generator>MSBuild:Compile</Generator>
   </MauiXaml>
   ```

### Services
- Empty `Services/` directory exists in shared project - add dependency injection services here
- Register services in `MauiProgramExtensions.UseSharedMauiApp()` using `builder.Services.AddSingleton/AddTransient`

### Navigation Between Pages
```csharp
// Navigate to absolute route
await Shell.Current.GoToAsync("//DashboardPage");

// Navigate back
await Shell.Current.GoToAsync("..");

// Navigate with parameters (register routes with Shell.Routing.RegisterRoute first)
await Shell.Current.GoToAsync($"Details?id={itemId}");
```

## Build & Debug

### Platform Selection
Build specific platform projects:
- Android: `SUS.EOS.NeoWallet.Droid.csproj`
- Windows: `SUS.EOS.NeoWallet.WinUI.csproj` (requires platform: x64, x86, or ARM64)
- iOS/Mac: Requires macOS build host

### Debug Configuration
Debug logging enabled via `#if DEBUG` in [MauiProgramExtensions.cs](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/MauiProgramExtensions.cs) using `Microsoft.Extensions.Logging.Debug`.

## Current State
- Active development: 10 pages implemented (9 wallet pages + MainPage navigation hub)
- **MainPage**: Navigation hub with wallet overview, quick actions, assets, and recent transactions
- **Shell Navigation**: Flyout menu enabled with Home, Wallet, and Account sections
- UI inspired by Anchor wallet (greymass/anchor) adapted for Neo blockchain
- **SUS.EOS.Sharp Library**: Modern .NET 10 blockchain library based on eos-sharp with improvements
- **Mock Services**: `MockWalletService` provides fake data for demonstration
- All pages have TODO markers for actual blockchain integration
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

### Service Organization
All NeoWallet services follow this structure:
- Interface in `Services/Interfaces/I{Name}.cs`
- Implementation in `Services/{Name}.cs`
- Models in `Services/Models/{Category}/`
- Registration in `MauiProgramExtensions.cs`

### Core Services

**IWalletStorageService** - Secure wallet storage:
- Load/save wallet with encryption
- Key management with password protection
- Secure preferences storage

**IWalletAccountService** - Account management:
- Add/remove wallet accounts
- Get account list
- Set active account
- Import from private key

**IWalletContextService** - Application-wide state:
- Active account tracking
- Active network tracking
- Initialization and state change events

**INetworkService** - Network management:
- Get available networks (WAX, EOS, Telos, etc.)
- Get default network
- Get network by chain ID

**ICryptographyService** - Encryption operations:
- Encrypt/decrypt with password
- Hash generation
- Key derivation

**IAnchorCallbackService** - Anchor protocol integration:
- Register callback handlers
- Execute callbacks
- Session persistence

**IEsrSessionManager** - ESR session lifecycle:
- WebSocket connection management
- dApp session tracking
- Real-time signing request handling

**IPriceFeedService** - Price data:
- Get token prices
- Cache management

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

### MainPage (Home)
- Wallet overview with balance and address
- Quick action buttons (Dashboard, Send, Receive, Import)
- Assets list with balances
- Recent transactions (3 most recent)
- Account management section
- **ESR Listener**: Automatically connects to WebSocket on appear

### Shell Flyout Menu
```
Home
  └─ Main (MainPage)
Wallet
  ├─ Dashboard (DashboardPage)
  ├─ Send (SendPage)
  └─ Receive (ReceivePage)
Account
  ├─ Create Account (CreateAccountPage)
  ├─ Import Wallet (ImportWalletPage)
  └─ Recover Account (RecoverAccountPage)
Developer Tools
  ├─ Contract Tables (ContractTablesPage)
  ├─ Contract Actions (ContractActionsPage)
  └─ Settings (SettingsPage)
Hidden Routes (IsVisible=False)
  ├─ Initialize (InitializePage)
  ├─ Wallet Setup (WalletSetupPage)
  └─ Enter Password (EnterPasswordPage)
```
- Assets list with balances
- Recent transactions (3 most recent)
- Account management section

### Shell Flyout Menu
```
Home
  └─ Main (MainPage)
Wallet
  ├─ Dashboard (DashboardPage)
  ├─ Send (SendPage)
  └─ Receive (ReceivePage)
Account
  ├─ Create Account (CreateAccountPage)
  ├─ Import Wallet (ImportWalletPage)
  └─ Recover Account (RecoverAccountPage)
Hidden Routes (IsVisible=False)
  ├─ Initialize (InitializePage)
  ├─ Wallet Setup (WalletSetupPage)
  └─ Enter Password (EnterPasswordPage)
```

## TODO Integration Points
- BIP39 seed phrase generation and validation
- Neo private key/address generation
- Wallet encryption/decryption
- Neo blockchain RPC communication
- QR code generation and scanning
- Transaction signing and broadcasting
- Balance and transaction history fetching

## Event Handling Best Practices

### Subscribe to Events in Constructor
```csharp
public MainPage(IWalletContextService walletContext, IEsrSessionManager esrManager)
{
    InitializeComponent();
    _walletContext = walletContext;
    _esrManager = esrManager;

    // Subscribe to context changes
    _walletContext.ActiveAccountChanged += OnActiveAccountChanged;
    _walletContext.ActiveNetworkChanged += OnActiveNetworkChanged;

    // Subscribe to ESR events
    _esrManager.SigningRequestReceived += OnEsrSigningRequestReceived;
    _esrManager.StatusChanged += OnEsrStatusChanged;
}
```

### Event Handler Pattern
```csharp
private async void OnEsrSigningRequestReceived(object? sender, EsrSigningRequestEventArgs e)
{
    try
    {
        System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ESR request received: {e.Request.ChainId}");
        await ProcessSigningRequestAsync(e);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"[MAINPAGE] Error: {ex.Message}");
        await DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
    }
}
```

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

## UI Event Handler Pattern
```csharp
private async void OnActionClicked(object sender, EventArgs e)
{
    try
    {
        LoadingIndicator.IsVisible = true;
        var result = await _service.PerformAsync();
        await DisplayAlert("Success", "Done!", "OK");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"[PAGE] Error: {ex.Message}");
        await DisplayAlert("Error", ex.Message, "OK");
    }
    finally
    {
        LoadingIndicator.IsVisible = false;
    }
}
```
