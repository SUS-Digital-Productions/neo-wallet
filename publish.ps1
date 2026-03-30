<#
.SYNOPSIS
    Build and publish Neo Wallet for one or more target platforms.

.DESCRIPTION
    1. Builds the React frontend (npm run build)
    2. Publishes the .NET backend as a single-file self-contained exe per target
    3. The frontend dist/ is embedded into wwwroot/ in the publish output
    4. Result: one folder per platform you can zip and distribute

.PARAMETER Runtime
    Target runtime identifier. Default: win-x64.
    Examples: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

.PARAMETER All
    Build all desktop platforms in one go (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64).

.PARAMETER Desktop
    Build all desktop platforms (same as -All).

.PARAMETER Windows
    Build all Windows targets (win-x64, win-arm64).

.PARAMETER Linux
    Build all Linux targets (linux-x64, linux-arm64).

.PARAMETER Mac
    Build all macOS targets (osx-x64, osx-arm64).

.PARAMETER Mobile
    Build mobile targets (Android APK via Capacitor). Requires Android SDK.
    iOS builds require macOS + Xcode and are skipped on other platforms.

.PARAMETER SkipFrontend
    Skip the frontend build step (useful when iterating on backend only).

.EXAMPLE
    .\publish.ps1                        # Default: win-x64
    .\publish.ps1 -Runtime linux-x64     # Single target
    .\publish.ps1 -All                   # All 6 desktop platforms
    .\publish.ps1 -Windows               # win-x64 + win-arm64
    .\publish.ps1 -Linux                 # linux-x64 + linux-arm64
    .\publish.ps1 -Mac                   # osx-x64 + osx-arm64
    .\publish.ps1 -Mobile                # Android (+ iOS on macOS)
    .\publish.ps1 -All -Mobile           # Everything
#>
param(
    [string]$Runtime = "",
    [switch]$All,
    [switch]$Desktop,
    [switch]$Windows,
    [switch]$Linux,
    [switch]$Mac,
    [switch]$Mobile,
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Resolve target list ─────────────────────────────────

$desktopTargets = @()

if ($All -or $Desktop) {
    $desktopTargets = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
}
else {
    if ($Windows) { $desktopTargets += @("win-x64", "win-arm64") }
    if ($Linux)   { $desktopTargets += @("linux-x64", "linux-arm64") }
    if ($Mac)     { $desktopTargets += @("osx-x64", "osx-arm64") }
}

# Default to win-x64 if nothing specified and no mobile-only
if ($desktopTargets.Count -eq 0 -and -not $Mobile) {
    if ($Runtime) {
        $desktopTargets = @($Runtime)
    }
    else {
        $desktopTargets = @("win-x64")
    }
}
elseif ($Runtime -and $desktopTargets.Count -eq 0) {
    $desktopTargets = @($Runtime)
}

$totalTargets = $desktopTargets.Count
if ($Mobile) { $totalTargets++ }

Write-Host "=== Neo Wallet Publish ===" -ForegroundColor Cyan
Write-Host "Targets: $($desktopTargets -join ', ')" -NoNewline
if ($Mobile) { Write-Host ", mobile (Android/iOS)" } else { Write-Host "" }
Write-Host ""

# ── Step 1: Build React frontend ────────────────────────

if (-not $SkipFrontend) {
    Write-Host "[frontend] Building React app..." -ForegroundColor Yellow
    Push-Location "$root\desktop\app"
    try {
        npm install --silent
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "Frontend build failed" }
    }
    finally {
        Pop-Location
    }
    Write-Host "[frontend] Done" -ForegroundColor Green
    Write-Host ""
}

# ── Step 2: Publish desktop targets ─────────────────────

$step = 0
foreach ($rid in $desktopTargets) {
    $step++
    Write-Host "[$step/$totalTargets] Publishing $rid..." -ForegroundColor Yellow

    $publishDir = "$root\publish\$rid"
    dotnet publish "$root\desktop\backend\NeoWallet.Backend.csproj" `
        --configuration Release `
        --runtime $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $publishDir

    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }

    # Copy platform-specific helper scripts
    if ($rid -like "win-*") {
        $setupScript = "$root\setup-hosts.cmd"
        if (Test-Path $setupScript) {
            Copy-Item $setupScript "$publishDir\setup-hosts.cmd"
        }
    }

    Write-Host "[$step/$totalTargets] $rid -> $publishDir" -ForegroundColor Green
}

# ── Step 3: Mobile builds (Capacitor) ───────────────────

if ($Mobile) {
    $step++
    Write-Host "[$step/$totalTargets] Building mobile targets..." -ForegroundColor Yellow
    Push-Location "$root\desktop\app"
    try {
        # Ensure Capacitor dependencies are installed
        if (-not (Test-Path "node_modules/@capacitor/core")) {
            Write-Host "  Installing Capacitor dependencies..." -ForegroundColor DarkYellow
            npm install @capacitor/core @capacitor/cli --save
            if ($LASTEXITCODE -ne 0) { throw "Capacitor install failed" }
        }

        # Sync web assets to native projects
        npx cap sync 2>$null

        # Android
        if (Test-Path "android") {
            Write-Host "  Building Android APK..." -ForegroundColor DarkYellow
            Push-Location "android"
            try {
                if ($IsWindows -or $env:OS -eq "Windows_NT") {
                    .\gradlew.bat assembleRelease
                }
                else {
                    ./gradlew assembleRelease
                }
                if ($LASTEXITCODE -ne 0) { throw "Android build failed" }

                $apkSource = "app\build\outputs\apk\release\app-release.apk"
                if (Test-Path $apkSource) {
                    $mobileDir = "$root\publish\android"
                    New-Item -ItemType Directory -Path $mobileDir -Force | Out-Null
                    Copy-Item $apkSource "$mobileDir\NeoWallet.apk"
                    Write-Host "  Android APK -> $mobileDir\NeoWallet.apk" -ForegroundColor Green
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Host "  Android project not initialized. Run: npx cap add android" -ForegroundColor DarkYellow
        }

        # iOS (macOS only)
        if (Test-Path "ios") {
            if ($IsMacOS -or (Test-Path "/usr/bin/xcodebuild")) {
                Write-Host "  Building iOS..." -ForegroundColor DarkYellow
                npx cap open ios
                Write-Host "  iOS project opened in Xcode. Build from there for signing." -ForegroundColor DarkYellow
            }
            else {
                Write-Host "  iOS builds require macOS + Xcode. Skipped." -ForegroundColor DarkYellow
            }
        }
        else {
            Write-Host "  iOS project not initialized. Run: npx cap add ios (on macOS)" -ForegroundColor DarkYellow
        }
    }
    finally {
        Pop-Location
    }
}

# ── Summary ──────────────────────────────────────────────

Write-Host ""
Write-Host "=== Publish Complete ===" -ForegroundColor Green

foreach ($rid in $desktopTargets) {
    $exeName = if ($rid -like "win-*") { "NeoWallet.Backend.exe" } else { "NeoWallet.Backend" }
    Write-Host "  $rid -> publish\$rid\$exeName"
}

if ($Mobile -and (Test-Path "$root\publish\android\NeoWallet.apk")) {
    Write-Host "  android -> publish\android\NeoWallet.apk"
}

Write-Host ""
Write-Host "Desktop apps open in a native window. Use --browser to open in a web browser instead."
