#Requires -Version 7.0

<#
.SYNOPSIS
    Multi-platform build script for SUS.EOS.NeoWallet
.DESCRIPTION
    Builds executable packages for Android, Windows, iOS, and macOS platforms.
    Creates distributable packages for each platform with proper signing and packaging.
.PARAMETER Platforms
    Comma-separated list of platforms to build: Android,Windows,iOS,macOS
    Default: Android,Windows
.PARAMETER Configuration
    Build configuration: Debug or Release
    Default: Release
.PARAMETER Clean
    Clean build output before building
.EXAMPLE
    .\build.ps1 -Platforms Android,Windows -Configuration Release
#>

param(
    [string]$Platforms = "Android,Windows",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean
)

# Configuration
$ErrorActionPreference = "Stop"
$SolutionDir = $PSScriptRoot
$SolutionFile = Join-Path $SolutionDir "SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.slnx"
$OutputDir = Join-Path $SolutionDir "build-output"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

# Project paths
$AndroidProject = Join-Path $SolutionDir "SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.Droid\SUS.EOS.NeoWallet.Droid.csproj"
$WindowsProject = Join-Path $SolutionDir "SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.WinUI\SUS.EOS.NeoWallet.WinUI.csproj"
$iOSProject = Join-Path $SolutionDir "SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.iOS\SUS.EOS.NeoWallet.iOS.csproj"
$MacProject = Join-Path $SolutionDir "SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.Mac\SUS.EOS.NeoWallet.Mac.csproj"

# Color output functions
function Write-Success { param($Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "`n═══ $Message ═══" -ForegroundColor Magenta }

# Banner
Write-Host @"

╔══════════════════════════════════════════════════════════╗
║     SUS.EOS.NeoWallet - Multi-Platform Build Script     ║
║                                                          ║
║  Platforms: $($Platforms.PadRight(42))║
║  Configuration: $($Configuration.PadRight(38))║
║  Output: build-output/                                   ║
╚══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Verify .NET SDK
Write-Step "Verifying Build Environment"
try {
    $dotnetVersion = dotnet --version
    Write-Success "Found .NET SDK version: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found. Please install .NET 10 SDK or later."
    exit 1
}

# Verify project files exist
Write-Info "Verifying project files..."
$requiredProjects = @()
$platformList = $Platforms -split ","
foreach ($platform in $platformList) {
    switch ($platform.Trim()) {
        "Android" { $requiredProjects += $AndroidProject }
        "Windows" { $requiredProjects += $WindowsProject }
        "iOS" { $requiredProjects += $iOSProject }
        "macOS" { $requiredProjects += $MacProject }
    }
}

foreach ($project in $requiredProjects) {
    if (Test-Path $project) {
        Write-Success "Found: $(Split-Path $project -Leaf)"
    } else {
        Write-Error "Project not found: $project"
        exit 1
    }
}

# Clean output directory
if ($Clean) {
    Write-Step "Cleaning Build Output"
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
        Write-Success "Cleaned output directory"
    }
}

# Create output directories
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir "logs") | Out-Null

# Restore NuGet packages
Write-Step "Restoring NuGet Packages"
$restoreLog = Join-Path $OutputDir "logs\restore-$Timestamp.log"
try {
    dotnet restore $SolutionFile > $restoreLog 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "NuGet packages restored successfully"
    } else {
        Write-Warning "Restore completed with warnings. Check log: $restoreLog"
    }
} catch {
    Write-Error "Failed to restore packages. Check log: $restoreLog"
    Get-Content $restoreLog | Select-Object -Last 20
    exit 1
}

# Build function
function Build-Platform {
    param(
        [string]$Platform,
        [string]$ProjectPath,
        [string]$Framework,
        [string]$RuntimeIdentifier = $null
    )
    
    Write-Step "Building $Platform ($Configuration)"
    
    $platformOutput = Join-Path $OutputDir $Platform
    New-Item -ItemType Directory -Force -Path $platformOutput | Out-Null
    
    $buildLog = Join-Path $OutputDir "logs\build-$Platform-$Timestamp.log"
    
    $buildArgs = @(
        "build",
        $ProjectPath,
        "-c", $Configuration,
        "-f", $Framework,
        "-o", $platformOutput
    )
    
    if ($RuntimeIdentifier) {
        $buildArgs += "-r", $RuntimeIdentifier
    }
    
    Write-Info "Project: $(Split-Path $ProjectPath -Leaf)"
    Write-Info "Framework: $Framework"
    if ($RuntimeIdentifier) {
        Write-Info "Runtime: $RuntimeIdentifier"
    }
    Write-Info "Output: $platformOutput"
    
    try {
        $buildOutput = & dotnet $buildArgs 2>&1
        $buildOutput | Out-File $buildLog -Encoding UTF8
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "$Platform build completed successfully"
            
            # Display output files
            $outputFiles = Get-ChildItem $platformOutput -File | Select-Object -First 5
            if ($outputFiles.Count -gt 0) {
                Write-Info "Generated files:"
                foreach ($file in $outputFiles) {
                    Write-Host "  • $($file.Name) ($([math]::Round($file.Length/1MB, 2)) MB)" -ForegroundColor Gray
                }
                if ((Get-ChildItem $platformOutput -File).Count > 5) {
                    Write-Host "  • ... and $((Get-ChildItem $platformOutput -File).Count - 5) more files" -ForegroundColor Gray
                }
            }
            
            return $true
        } else {
            Write-Error "$Platform build failed. Check log: $buildLog"
            $buildOutput | Select-String "error" | Select-Object -First 10 | ForEach-Object {
                Write-Host "  $_" -ForegroundColor Red
            }
            return $false
        }
    } catch {
        Write-Error "Exception during $Platform build: $_"
        return $false
    }
}

# Build packages function
function Package-Platform {
    param(
        [string]$Platform,
        [string]$ProjectPath
    )
    
    Write-Step "Packaging $Platform"
    
    $packageOutput = Join-Path $OutputDir "$Platform-Package"
    New-Item -ItemType Directory -Force -Path $packageOutput | Out-Null
    
    $packageLog = Join-Path $OutputDir "logs\package-$Platform-$Timestamp.log"
    
    switch ($Platform) {
        "Android" {
            # Build APK
            Write-Info "Creating Android APK..."
            $publishArgs = @(
                "publish",
                $ProjectPath,
                "-c", $Configuration,
                "-f", "net10.0-android",
                "-o", $packageOutput
            )
            
            & dotnet $publishArgs > $packageLog 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $apkFiles = Get-ChildItem $packageOutput -Filter "*.apk" -Recurse
                if ($apkFiles.Count -gt 0) {
                    Write-Success "APK created: $($apkFiles[0].FullName)"
                    Write-Info "Size: $([math]::Round($apkFiles[0].Length/1MB, 2)) MB"
                } else {
                    Write-Warning "APK file not found in output"
                }
            } else {
                Write-Error "Android packaging failed. Check log: $packageLog"
            }
        }
        
        "Windows" {
            # Create MSIX package
            Write-Info "Creating Windows MSIX package..."
            $publishArgs = @(
                "publish",
                $ProjectPath,
                "-c", $Configuration,
                "-f", "net10.0-windows10.0.19041.0",
                "-r", "win-x64",
                "-p:PublishProfile=win-x64",
                "-o", $packageOutput
            )
            
            & dotnet $publishArgs > $packageLog 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $msixFiles = Get-ChildItem $packageOutput -Filter "*.msix" -Recurse
                if ($msixFiles.Count -gt 0) {
                    Write-Success "MSIX created: $($msixFiles[0].FullName)"
                    Write-Info "Size: $([math]::Round($msixFiles[0].Length/1MB, 2)) MB"
                } else {
                    # Also check for AppX files
                    $appxFiles = Get-ChildItem $packageOutput -Filter "*.appx" -Recurse
                    if ($appxFiles.Count -gt 0) {
                        Write-Success "AppX created: $($appxFiles[0].FullName)"
                        Write-Info "Size: $([math]::Round($appxFiles[0].Length/1MB, 2)) MB"
                    } else {
                        Write-Warning "Package file not found in output"
                    }
                }
            } else {
                Write-Error "Windows packaging failed. Check log: $packageLog"
            }
        }
        
        "iOS" {
            Write-Info "Creating iOS IPA..."
            Write-Warning "iOS packaging requires macOS with Xcode"
            Write-Warning "Run this on macOS or use cloud build service"
            # iOS requires code signing, which needs Apple Developer account
            # This is typically done through Xcode or cloud services
        }
        
        "macOS" {
            Write-Info "Creating macOS app bundle..."
            Write-Warning "macOS packaging requires macOS with Xcode"
            Write-Warning "Run this on macOS or use cloud build service"
            # macOS also requires code signing
        }
    }
}

# Build each platform
$buildResults = @{}
foreach ($platform in $platformList) {
    $platform = $platform.Trim()
    
    $buildSuccess = switch ($platform) {
        "Android" {
            Build-Platform -Platform "Android" -ProjectPath $AndroidProject -Framework "net10.0-android"
        }
        "Windows" {
            Build-Platform -Platform "Windows" -ProjectPath $WindowsProject -Framework "net10.0-windows10.0.19041.0" -RuntimeIdentifier "win-x64"
        }
        "iOS" {
            if ($IsMacOS) {
                Build-Platform -Platform "iOS" -ProjectPath $iOSProject -Framework "net10.0-ios"
            } else {
                Write-Warning "iOS builds require macOS. Skipping..."
                $false
            }
        }
        "macOS" {
            if ($IsMacOS) {
                Build-Platform -Platform "macOS" -ProjectPath $MacProject -Framework "net10.0-macos"
            } else {
                Write-Warning "macOS builds require macOS. Skipping..."
                $false
            }
        }
        default {
            Write-Error "Unknown platform: $platform"
            $false
        }
    }
    
    $buildResults[$platform] = $buildSuccess
}

# Package successful builds
Write-Host ""
Write-Step "Packaging Builds"
foreach ($platform in $buildResults.Keys) {
    if ($buildResults[$platform]) {
        switch ($platform) {
            "Android" { Package-Platform -Platform "Android" -ProjectPath $AndroidProject }
            "Windows" { Package-Platform -Platform "Windows" -ProjectPath $WindowsProject }
            "iOS" { if ($IsMacOS) { Package-Platform -Platform "iOS" -ProjectPath $iOSProject } }
            "macOS" { if ($IsMacOS) { Package-Platform -Platform "macOS" -ProjectPath $MacProject } }
        }
    }
}

# Summary
Write-Host ""
Write-Step "Build Summary"
Write-Host ""

$successCount = ($buildResults.Values | Where-Object { $_ -eq $true }).Count
$totalCount = $buildResults.Count

Write-Host "Build Results:" -ForegroundColor Cyan
foreach ($platform in $buildResults.Keys) {
    if ($buildResults[$platform]) {
        Write-Host "  ✓ $platform" -ForegroundColor Green -NoNewline
        Write-Host " - SUCCESS" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ $platform" -ForegroundColor Red -NoNewline
        Write-Host " - FAILED" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Output Location:" -ForegroundColor Cyan
Write-Host "  $OutputDir" -ForegroundColor Gray

Write-Host ""
Write-Host "Logs Location:" -ForegroundColor Cyan
Write-Host "  $(Join-Path $OutputDir "logs")" -ForegroundColor Gray

Write-Host ""
if ($successCount -eq $totalCount) {
    Write-Success "All builds completed successfully! ($successCount/$totalCount)"
    exit 0
} else {
    Write-Warning "Some builds failed. ($successCount/$totalCount succeeded)"
    Write-Info "Check log files for details"
    exit 1
}
