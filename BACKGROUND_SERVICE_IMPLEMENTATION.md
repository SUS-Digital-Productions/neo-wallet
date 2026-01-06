# Background Service Implementation

## Overview
Implemented Windows system tray functionality so NeoWallet runs in the background even when "closed", allowing it to respond to ESR protocol activation (esr://, anchor://) at any time.

## Problem Solved
Users need the wallet to be always available to handle "Launch Anchor" requests from dApps, similar to how the official Anchor wallet works. When users close the window, the app should minimize to system tray instead of exiting completely.

## Implementation

### 1. System Tray Service

Created `ISystemTrayService` interface and `SystemTrayService` implementation using Win32 `NotifyIcon` API.

**Files Created:**
- `Services/Interfaces/ISystemTrayService.cs` - Cross-platform interface
- `Services/Interfaces/ISystemTrayService.Platform.cs` - Platform-specific extensions
- `Services/SystemTrayService.cs` - Win32 implementation

**Key Features:**
- System tray icon with custom tooltip
- Hide/show main window
- Notification balloons
- Exit application functionality

**Win32 APIs Used:**
```csharp
Shell_NotifyIcon()  // Add/modify/remove tray icon
ShowWindow()        // Show/hide window
SetForegroundWindow() // Bring window to front
LoadIcon()          // Load application icon
```

### 2. App.xaml.cs Integration

**Changes:**
1. Added `ISystemTrayService` dependency injection
2. Stored reference to main `Window`
3. Platform-specific window configuration
4. Intercepted window close event to minimize to tray

**Key Methods:**

```csharp
private void ConfigureWindowForBackground(Window window)
{
#if WINDOWS
    var platformWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
    _trayService.SetNativeWindow(platformWindow);
    _trayService.Initialize();
    
    var appWindow = AppWindow.GetFromWindowId(...);
    appWindow.Closing += OnWindowClosing;
#endif
}

private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
{
    args.Cancel = true;  // Cancel the close
    _trayService.HideToTray();  // Hide to tray instead
}
```

**Protocol Activation Enhancement:**
```csharp
protected override void OnAppLinkRequestReceived(Uri uri)
{
    base.OnAppLinkRequestReceived(uri);
    
    // Show window if hidden
    _trayService.ShowMainWindow();
    
    // Process ESR...
}
```

### 3. Service Registration

Added to [MauiProgramExtensions.cs](SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/SUS.EOS.NeoWallet/MauiProgramExtensions.cs):

```csharp
builder.Services.AddSingleton<ISystemTrayService, SystemTrayService>();
```

## Behavior

### Normal Operation
1. **First Launch**: App starts normally, shows initialization/password screen
2. **Close Button**: Clicking X minimizes to system tray (doesn't exit)
3. **Tray Icon**: Visible in Windows notification area with "NeoWallet - Running in background" tooltip
4. **Notification**: Shows balloon notification: "Running in background. Click tray icon to restore."

### Protocol Activation (esr:// or anchor://)
1. **ESR Link Triggered**: User clicks "Launch Anchor" on dApp
2. **OS Launches App**: Windows activates NeoWallet via protocol handler
3. **Window Restored**: `ShowMainWindow()` called automatically
4. **ESR Processed**: Signing popup appears for user approval

### Exiting Completely
Currently, to exit the app completely, you need to:
- **TODO**: Add right-click context menu to tray icon with "Exit" option
- **Workaround**: Use Task Manager or Dev Tools

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| Windows  | ✅ Implemented | Full tray icon support |
| Android  | ⏳ Planned | Foreground service with notification |
| iOS      | ⏳ Planned | Background modes configuration |
| macOS    | ⏳ Planned | NSStatusBar implementation |

## Testing

### Build and Deploy
```powershell
cd "c:\Users\pasag\Desktop\SUS Projects\neo-wallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet"
dotnet build SUS.EOS.NeoWallet.WinUI/SUS.EOS.NeoWallet.WinUI.csproj -p:Platform=x64
```

### Test Scenarios

**1. Window Close Behavior**
- Launch NeoWallet
- Click the X button
- Expected: Window hides, tray icon appears, notification shows
- Verify: App still running in Task Manager

**2. Tray Icon Click** (TODO - not yet implemented)
- Right-click tray icon
- Expected: Context menu with "Show" and "Exit" options

**3. Protocol Activation While Hidden**
- Hide NeoWallet to tray
- Trigger ESR link: `Start-Process "esr://..."`
- Expected: Window restores automatically, signing popup appears

**4. Background ESR Listener**
- App running in tray
- ESR session WebSocket active
- Expected: Receives signing requests even when hidden

### Debug Output
```
[APP] Configuring Windows-specific handlers
[TRAY] Native window set
[TRAY] Initializing system tray icon
[TRAY] System tray icon created
[APP] Background mode configured

[APP] Window closing - minimizing to tray
[TRAY] HideToTray called
[TRAY] Showing notification: NeoWallet

[APP] Deep link received: esr://...
[TRAY] ShowMainWindow called
```

## TODOs and Enhancements

### High Priority

1. **Tray Context Menu** (CRITICAL)
```csharp
// Add to SystemTrayService.cs
private void CreateContextMenu()
{
    // Show/Hide window
    // Exit application
    // Settings
}
```

2. **Tray Icon Click Handler**
```csharp
// Detect left-click vs right-click on tray icon
// Left-click: Show window
// Right-click: Show context menu
```

3. **Auto-Start on Windows Login**
```csharp
// Add registry key:
// HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
// Key: "NeoWallet"
// Value: Path to executable
```

### Medium Priority

4. **Custom Tray Icon**
- Create .ico file with app branding
- Embed in project resources
- Load via `LoadIcon()` or `Icon.ExtractAssociatedIcon()`

5. **Tray Notification Actions**
```csharp
// Windows 10/11 Toast Notifications
// Clickable actions in notification
```

6. **Minimize on Start** (Optional)
```csharp
// Add app setting: "Start minimized to tray"
// Skip showing window on startup if enabled
```

### Low Priority

7. **Multi-Window Support**
- Track multiple windows
- Restore all windows on tray click

8. **Tray Animation**
- Animated icon for active signing requests
- Visual feedback for pending actions

## Known Issues

1. **No Exit Option**: Currently no graceful way to exit app from tray (must use Task Manager)
   - **Impact**: High
   - **Fix**: Implement context menu with Exit option

2. **Icon Not Visible**: Default system icon used, may not be visible on all Windows themes
   - **Impact**: Medium
   - **Fix**: Create and embed custom .ico file

3. **Single Click Ignored**: Tray icon doesn't respond to left-click
   - **Impact**: Medium
   - **Fix**: Implement `WM_TRAYICON` message handling for clicks

4. **Memory Usage**: App keeps full UI in memory even when hidden
   - **Impact**: Low
   - **Mitigation**: MAUI apps are lightweight, negligible impact

## Architecture Notes

### Why Win32 NotifyIcon Instead of WinUI TrayIcon?

WinUI 3 doesn't have built-in system tray support (unlike WPF's `NotifyIcon`). Options were:
1. **Win32 Shell_NotifyIcon API** (chosen) - Direct, lightweight, works
2. **CommunityToolkit.WinUI.Notifications** - Requires NuGet package, more complex
3. **WinForms NotifyIcon** - Would require Windows Forms interop

Chose Win32 API for:
- No additional dependencies
- Full control
- Maximum compatibility
- Production-ready (used by most Windows apps)

### Platform Abstraction Pattern

Interface defined with cross-platform methods:
```csharp
public partial interface ISystemTrayService
{
    void Initialize();
    void ShowMainWindow();
    void HideToTray();
}
```

Platform-specific extensions via partial interface:
```csharp
#if WINDOWS
public partial interface ISystemTrayService
{
    void SetNativeWindow(Microsoft.UI.Xaml.Window window);
}
#endif
```

Implementation has platform-specific code blocks:
```csharp
public partial class SystemTrayService
{
#if WINDOWS
    // Windows implementation
#else
    // No-op for other platforms
#endif
}
```

## References

- [Win32 Shell_NotifyIcon API](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw)
- [MAUI Windows Platform Integration](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/)
- [WinUI 3 Window Management](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing)
- [AppWindow Class](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow)
