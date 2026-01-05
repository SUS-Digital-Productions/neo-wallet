# SUS.EOS.NeoWallet - AI Coding Instructions

## Project Overview
This is a cross-platform Neo blockchain wallet built with .NET MAUI targeting .NET 10.0. The solution follows a single-project architecture pattern with platform-specific head projects inspired by the Anchor wallet (greymass/anchor).

## Architecture Pattern

### Project Structure
- **SUS.EOS.NeoWallet** - Shared core project containing all UI, business logic, and cross-platform code
- **SUS.EOS.NeoWallet.Droid** - Android platform head (targets `net10.0-android`, min SDK 21)
- **SUS.EOS.NeoWallet.iOS** - iOS platform head
- **SUS.EOS.NeoWallet.Mac** - macOS platform head
- **SUS.EOS.NeoWallet.WinUI** - Windows platform head (targets `net10.0-windows10.0.19041.0`)

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

## Code Conventions

### Namespaces
- Root namespace: `SUS.EOS.NeoWallet`
- Pages use file-scoped namespaces: `namespace SUS.EOS.NeoWallet.Pages;`
- Platform-specific code may use nested namespaces (e.g., `SUS.EOS.NeoWallet.Droid`)

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

### Architecture
- **Location**: `SUS.EOS.Sharp/` project (referenced by main wallet project)
- **Target**: .NET 10 class library
- **Design**: Modern C# with records, async/await, cancellation tokens, IDisposable

### Key Classes
- `EosClient`: Main blockchain client (chainInfo, account, balance, transactions)
- `EosConfiguration`: Immutable configuration record
- Models: `Transaction`, `SignedTransaction`, `Account`, `Asset`, `ChainInfo`
- Providers: `ISignatureProvider`, `IAbiSerializationProvider` interfaces

### Usage Pattern
```csharp
var config = new EosConfiguration
{
    HttpEndpoint = "https://nodes.eos42.io",
    ChainId = "...",
    ExpireSeconds = 30
};

using var client = new EosClient(config);
var info = await client.GetInfoAsync(cancellationToken);
var account = await client.GetAccountAsync("myaccount", cancellationToken);
```

### Asset Parsing
```csharp
var asset = Asset.Parse("100.0000 EOS");
Console.WriteLine(asset.Amount);    // 100.0000
Console.WriteLine(asset.Symbol);    // EOS
Console.WriteLine(asset.Precision); // 4
Console.WriteLine(asset);           // "100.0000 EOS"
```

## Services Layer

### IWalletService
- `GetBalanceAsync()`: Returns main wallet balance
- `GetAssetsAsync()`: Returns all wallet assets
- `GetTransactionHistoryAsync(count)`: Returns recent transactions
- `GetAddressAsync()`: Returns wallet address
- `SendAsync(toAddress, amount, memo)`: Sends transaction

### MockWalletService
- Registered as singleton in DI container
- Pre-populated with mock NEO, GAS, EOS, USDT assets
- 6 mock transactions with varied timestamps
- Simulates 1-second network delay on send

## Navigation Structure

### MainPage (Home)
- Wallet overview with balance and address
- Quick action buttons (Dashboard, Send, Receive, Import)
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
