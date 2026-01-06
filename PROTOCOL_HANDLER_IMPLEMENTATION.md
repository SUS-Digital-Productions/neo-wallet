# Protocol Handler Implementation for Deep Linking

## Overview
Implemented OS-level protocol handler registration for `esr://` and `anchor://` URLs to enable "Launch Anchor" button functionality from dApps like WAX Bloks.

## Problem Analysis

### Anchor Wallet Behavior
When users click "Launch Anchor" on WAX Bloks:
1. Browser triggers `esr://` or `anchor://` protocol link
2. OS launches the registered wallet app
3. Wallet processes the ESR signing request
4. Response sent via WebSocket (for session-based) or HTTP callback
5. Browser page auto-updates (no popup window)

### Our Implementation Status
- ✅ **Protocol Registration**: Added to `Package.appxmanifest` for Windows
- ✅ **Deep Link Handler**: Implemented in `App.xaml.cs`
- ✅ **ESR Processing**: MainPage handles incoming ESR URLs
- ⏳ **Android Support**: TODO - Add intent-filter to AndroidManifest.xml

## Implementation Details

### 1. Windows Protocol Registration (Package.appxmanifest)

Added protocol extensions:

```xml
<Extensions>
  <uap:Extension Category="windows.protocol">
    <uap:Protocol Name="esr">
      <uap:DisplayName>EOSIO Signing Request</uap:DisplayName>
    </uap:Protocol>
  </uap:Extension>
  <uap:Extension Category="windows.protocol">
    <uap:Protocol Name="anchor">
      <uap:DisplayName>Anchor Link</uap:DisplayName>
    </uap:Protocol>
  </uap:Extension>
</Extensions>
```

**Location**: `SUS.EOS.NeoWallet.WinUI/Package.appxmanifest`

### 2. App.xaml.cs - Protocol Activation Handler

Overrode `OnAppLinkRequestReceived` to intercept deep links:

```csharp
protected override void OnAppLinkRequestReceived(Uri uri)
{
    base.OnAppLinkRequestReceived(uri);
    
    System.Diagnostics.Trace.WriteLine($"[APP] Deep link received: {uri}");
    
    // Handle esr:// or anchor:// protocol
    if (uri.Scheme == "esr" || uri.Scheme == "anchor")
    {
        System.Diagnostics.Trace.WriteLine($"[APP] Processing ESR link: {uri}");
        _ = HandleEsrLinkAsync(uri.ToString());
    }
}

private async Task HandleEsrLinkAsync(string esrUri)
{
    try
    {
        System.Diagnostics.Trace.WriteLine($"[APP] HandleEsrLinkAsync: {esrUri}");
        
        // Wait for app to be ready
        await Task.Delay(500);
        
        // Get MainPage (or current page)
        if (Application.Current?.MainPage is AppShell shell)
        {
            // Navigate to MainPage if not already there
            await shell.GoToAsync("//MainPage");
            
            // Find MainPage and trigger ESR handling
            if (shell.CurrentPage is MainPage mainPage)
            {
                System.Diagnostics.Trace.WriteLine("[APP] Found MainPage, processing ESR");
                await mainPage.HandleDeepLinkEsrAsync(esrUri);
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"[APP] Error handling ESR link: {ex.Message}");
    }
}
```

**Key Points**:
- Added `using SUS.EOS.NeoWallet.Pages;` to resolve MainPage type
- Navigates to MainPage before processing ESR
- Passes ESR URL to MainPage handler

### 3. MainPage.xaml.cs - ESR Deep Link Processing

Added public method to handle deep link ESRs:

```csharp
/// <summary>
/// Handle ESR deep link from protocol activation (esr:// or anchor://)
/// </summary>
public async Task HandleDeepLinkEsrAsync(string esrUri)
{
    try
    {
        System.Diagnostics.Trace.WriteLine($"[MAINPAGE] HandleDeepLinkEsrAsync: {esrUri}");

        // Parse the ESR
        var esrService = _serviceProvider.GetRequiredService<IEsrService>();
        var esrRequest = await esrService.ParseRequestAsync(esrUri);

        System.Diagnostics.Trace.WriteLine(
            $"[MAINPAGE] ESR parsed successfully. ChainId: {esrRequest.ChainId}"
        );

        // Create EventArgs to pass to signing popup
        var eventArgs = new EsrSigningRequestEventArgs
        {
            Request = esrRequest,
            Session = null, // No session for deep link ESR
            Callback = null, // No callback URL in envelope for deep link ESR
            IsIdentityRequest =
                !esrRequest.Payload.IsTransaction && !esrRequest.Payload.IsAction
        };

        // Show signing popup
        await ShowSigningPopupAsync(eventArgs);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine(
            $"[MAINPAGE] Error handling deep link ESR: {ex.Message}"
        );
        System.Diagnostics.Trace.WriteLine($"[MAINPAGE] Stack trace: {ex.StackTrace}");
        await DisplayAlert("Error", $"Failed to process ESR link: {ex.Message}", "OK");
    }
}
```

**Key Points**:
- Parses ESR using `IEsrService`
- Creates `EsrSigningRequestEventArgs` with proper identity detection
- Reuses existing `ShowSigningPopupAsync` method

## Testing Instructions

### Windows Testing

1. **Build and Deploy**:
   ```powershell
   cd "c:\Users\pasag\Desktop\SUS Projects\neo-wallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet"
   dotnet build SUS.EOS.NeoWallet.WinUI/SUS.EOS.NeoWallet.WinUI.csproj -p:Platform=x64
   ```

2. **Install App** (to register protocols):
   - Deploy via Visual Studio OR
   - Use `Add-AppxPackage` to install the built .msix package

3. **Test Protocol Activation**:
   - Open PowerShell
   - Run: `Start-Process "esr://g2PgYmZYwSxwsWLXQwYm64ySkoJiK3395CS9xLzkjPwivZzMvGx9Q3MTQ7PkNAvdREtzS10TA..."`
   - NeoWallet should open and show signing popup

4. **Test WAX Bloks**:
   - Go to https://wax.bloks.io/
   - Click "Login"
   - Select "Anchor" wallet
   - Click "Launch Anchor" button
   - NeoWallet should open (if it's the registered handler)

### Debug Output to Monitor

When protocol handler is triggered:
```
[APP] Deep link received: esr://g2PgYmZY...
[APP] Processing ESR link: esr://g2PgYmZY...
[APP] HandleEsrLinkAsync: esr://g2PgYmZY...
[APP] Found MainPage, processing ESR
[MAINPAGE] HandleDeepLinkEsrAsync: esr://g2PgYmZY...
[MAINPAGE] ESR parsed successfully. ChainId: 1064487b3cd1a897...
[MAINPAGE] Creating EsrSigningPopupPage...
```

## Next Steps

### 1. Android Protocol Handler (HIGH PRIORITY)

Add to `AndroidManifest.xml`:

```xml
<activity android:name=".MainActivity">
  <intent-filter>
    <action android:name="android.intent.action.VIEW" />
    <category android:name="android.intent.category.DEFAULT" />
    <category android:name="android.intent.category.BROWSABLE" />
    <data android:scheme="esr" />
  </intent-filter>
  <intent-filter>
    <action android:name="android.intent.action.VIEW" />
    <category android:name="android.intent.category.DEFAULT" />
    <category android:name="android.intent.category.BROWSABLE" />
    <data android:scheme="anchor" />
  </intent-filter>
</activity>
```

Handle intent in `MainActivity.cs`:

```csharp
protected override void OnNewIntent(Intent intent)
{
    base.OnNewIntent(intent);
    
    if (intent?.Data != null)
    {
        var uri = intent.Data.ToString();
        System.Diagnostics.Trace.WriteLine($"[MAINACTIVITY] Deep link: {uri}");
        
        // Pass to App's handler
        Microsoft.Maui.ApplicationModel.Platform.OnNewIntent(intent);
    }
}
```

### 2. iOS Protocol Handler

Add to `Info.plist`:

```xml
<key>CFBundleURLTypes</key>
<array>
  <dict>
    <key>CFBundleURLName</key>
    <string>EOSIO Signing Request</string>
    <key>CFBundleURLSchemes</key>
    <array>
      <string>esr</string>
      <string>anchor</string>
    </array>
  </dict>
</array>
```

### 3. WebSocket Session Handling

For session-based ESR (Anchor Link protocol):
- Check if `esrRequest.Info` contains `"link"` key (channel info)
- If present, establish WebSocket connection to that channel
- Send response via WebSocket instead of HTTP callback
- Store session for future requests

### 4. Background Service (Optional)

For "always listening" behavior like Anchor:
- Windows: Background task registration
- Android: Foreground service with notification
- iOS: Background modes configuration

## Known Issues

1. **Multiple Handlers**: If Anchor wallet is also installed, both apps will be registered for `esr://` protocol. Windows will prompt user to choose.

2. **WebSocket Pre-Connection**: Current implementation doesn't maintain persistent WebSocket connection. Session-based ESR will need channel info from ESR to connect.

3. **App Launch Delay**: 500ms delay added to ensure app initialization completes before processing ESR.

## References

- [EOSIO Signing Request Spec](https://github.com/greymass/eosio-signing-request)
- [Anchor Link Protocol](https://github.com/greymass/anchor-link)
- [MAUI Deep Linking](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/applinks)
- [Windows Protocol Handlers](https://learn.microsoft.com/en-us/windows/uwp/launch-resume/handle-uri-activation)
