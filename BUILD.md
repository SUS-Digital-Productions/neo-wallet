# Build Instructions

## Prerequisites

### All Platforms
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- PowerShell 7.0+ (for build script)

### Android
- Android SDK (installed via Visual Studio or Android Studio)
- Java JDK 17 or later

### Windows
- Windows 10/11
- Visual Studio 2022 with Windows App SDK workload

### iOS/macOS (requires macOS)
- Xcode 15 or later
- Apple Developer account (for code signing)

## Quick Start

### Windows (Batch File)
```cmd
# Build Android + Windows (Release)
build.bat

# Build only Android (Debug)
build.bat Android Debug

# Build all platforms (Release)
build.bat Android,Windows,iOS,macOS Release
```

### PowerShell Script (All Platforms)
```powershell
# Build Android + Windows (default)
.\build.ps1

# Build specific platforms
.\build.ps1 -Platforms Android,Windows -Configuration Release

# Clean build
.\build.ps1 -Platforms Android -Configuration Release -Clean

# Build with debug symbols
.\build.ps1 -Platforms Windows -Configuration Debug
```

## Platform-Specific Builds

### Android APK
```powershell
.\build.ps1 -Platforms Android -Configuration Release
```
Output: `build-output/Android-Package/*.apk`

Minimum SDK: API 21 (Android 5.0)
Target SDK: Latest

### Windows MSIX
```powershell
.\build.ps1 -Platforms Windows -Configuration Release
```
Output: `build-output/Windows-Package/*.msix`

Requires: Windows 10.0.19041.0 or later

### iOS IPA
```powershell
# Must be run on macOS
.\build.ps1 -Platforms iOS -Configuration Release
```
Output: `build-output/iOS-Package/*.ipa`

Note: Requires Apple Developer account and code signing certificates

### macOS App Bundle
```powershell
# Must be run on macOS
.\build.ps1 -Platforms macOS -Configuration Release
```
Output: `build-output/macOS-Package/*.app`

Note: Requires Apple Developer account and code signing certificates

## Build Output Structure

```
neo-wallet/
├── build-output/
│   ├── Android/           # Android build artifacts
│   ├── Windows/           # Windows build artifacts
│   ├── iOS/               # iOS build artifacts (macOS only)
│   ├── macOS/             # macOS build artifacts (macOS only)
│   ├── Android-Package/   # Distributable APK
│   ├── Windows-Package/   # Distributable MSIX
│   └── logs/              # Build logs
│       ├── restore-*.log
│       ├── build-*.log
│       └── package-*.log
```

## Troubleshooting

### Build Fails on Windows
1. Ensure Windows App SDK is installed:
   ```cmd
   winget install Microsoft.WindowsAppSDK
   ```

2. Check platform toolset:
   ```powershell
   dotnet workload list
   ```

### Android Build Issues
1. Install Android workload:
   ```cmd
   dotnet workload install android
   ```

2. Set ANDROID_HOME environment variable:
   ```cmd
   set ANDROID_HOME=C:\Program Files (x86)\Android\android-sdk
   ```

### iOS/macOS Build Issues (macOS only)
1. Install iOS/macOS workloads:
   ```bash
   dotnet workload install ios
   dotnet workload install macos
   ```

2. Open Xcode and accept license agreements:
   ```bash
   sudo xcode-select --switch /Applications/Xcode.app
   sudo xcodebuild -license accept
   ```

### Clean Build
If you encounter strange build errors, try a clean build:
```powershell
.\build.ps1 -Clean -Platforms Android,Windows
```

## Manual Build (Advanced)

### Android
```cmd
dotnet build SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.Droid\SUS.EOS.NeoWallet.Droid.csproj -c Release -f net10.0-android
dotnet publish SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.Droid\SUS.EOS.NeoWallet.Droid.csproj -c Release -f net10.0-android -o build-output/Android-Manual
```

### Windows
```cmd
dotnet build SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.WinUI\SUS.EOS.NeoWallet.WinUI.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64
dotnet publish SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.WinUI\SUS.EOS.NeoWallet.WinUI.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 -o build-output/Windows-Manual
```

## Distribution

### Android
1. Sign the APK with your keystore
2. Upload to Google Play Store or distribute directly

### Windows
1. Sign the MSIX with a code signing certificate
2. Upload to Microsoft Store or sideload on Windows devices

### iOS
1. Build and sign with Apple Developer certificate
2. Upload to App Store Connect using Xcode or Transporter

### macOS
1. Build and sign with Apple Developer certificate
2. Notarize the app bundle
3. Upload to Mac App Store or distribute as DMG

## CI/CD Integration

### GitHub Actions Example
```yaml
name: Build Multi-Platform

on: [push, pull_request]

jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Build (Windows)
        if: matrix.os == 'windows-latest'
        shell: pwsh
        run: .\build.ps1 -Platforms Android,Windows -Configuration Release
      
      - name: Build (macOS)
        if: matrix.os == 'macos-latest'
        shell: pwsh
        run: .\build.ps1 -Platforms iOS,macOS -Configuration Release
      
      - name: Upload Artifacts
dw        uses: actions/upload-artifact@v4
        with:
          name: build-output-${{ matrix.os }}
          path: build-output/
```

## Support

For build issues:
1. Check logs in `build-output/logs/`
2. Verify all prerequisites are installed
3. Try a clean build with `-Clean` flag
4. Check Visual Studio workloads are installed

For platform-specific issues:
- Android: Check Android SDK installation
- Windows: Verify Windows App SDK is installed
- iOS/macOS: Ensure Xcode is properly configured
