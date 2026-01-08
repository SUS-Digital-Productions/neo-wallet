@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ==========================================================
REM CONFIGURATION
REM ==========================================================

set CONFIGURATION=Release
set ROOT=%~dp0
set ARTIFACTS=%ROOT%artifacts
set LOGS=%ARTIFACTS%\logs

REM Check for solution file
if exist "%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.sln" (
    set SOLUTION=%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.sln
) else if exist "%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.slnx" (
    set SOLUTION=%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.slnx
) else (
    echo ERROR: No solution file found
    exit /b 1
)

set WINDOWS_PROJECT=%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.WinUI\SUS.EOS.NeoWallet.WinUI.csproj
set ANDROID_PROJECT=%ROOT%SUS.EOS.NeoWallet\SUS.EOS.NeoWallet\SUS.EOS.NeoWallet.Droid\SUS.EOS.NeoWallet.Droid.csproj

set CERT_NAME=NeoWallet Dev MSIX
set CERT_PFX=%ROOT%NeoWallet-Dev.pfx
set CERT_PASSWORD=devpassword

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set TS=%%i

REM ==========================================================
REM BANNER
REM ==========================================================

echo.
echo ==========================================================
echo  SUS.EOS.NeoWallet - MAUI Build Script (.NET 10)
echo ==========================================================
echo  Configuration: %CONFIGURATION%
echo  Output: %ARTIFACTS%
echo ==========================================================
echo.

REM ==========================================================
REM CHECK DOTNET VERSION
REM ==========================================================

echo Checking .NET SDK...
for /f "delims=" %%v in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%v
if "%DOTNET_VERSION%"=="" (
    echo ERROR: .NET SDK not found or not in PATH
    exit /b 1
)
echo Found .NET SDK version: %DOTNET_VERSION%

echo.
echo Checking .NET workloads...
dotnet workload list | findstr /i "maui android ios macos"
if errorlevel 1 (
    echo WARNING: MAUI workloads may not be installed
    echo.
    set /p CONTINUE="Continue anyway? (y/n): "
    if /i not "!CONTINUE!"=="y" (
        exit /b 1
    )
)

REM ==========================================================
REM CLEAN
REM ==========================================================

if "%1"=="clean" (
    echo Cleaning artifacts and obj/bin folders...
    if exist "%ARTIFACTS%" rmdir /s /q "%ARTIFACTS%"
    goto :skip_clean
)

if exist "%ARTIFACTS%" (
    echo Cleaning artifacts folder...
    rmdir /s /q "%ARTIFACTS%"
)

:skip_clean
mkdir "%ARTIFACTS%" 2>nul
mkdir "%LOGS%" 2>nul

REM ==========================================================
REM ADMIN: Trust certificate at Machine level
REM Usage: build.bat trust-machine
REM This will prompt for UAC and import the PFX into LocalMachine\My
REM and add the public certificate to LocalMachine\Root
REM ==========================================================
if "%1"=="trust-machine" (
    echo Preparing to trust certificate at machine level (requires admin)...

    set PSFILE=%TEMP%\trust-msix-cert.ps1
    echo $pwd = ConvertTo-SecureString -String '%CERT_PASSWORD%' -AsPlainText -Force > "%PSFILE%"
    echo Import-PfxCertificate -FilePath '%CERT_PFX%' -CertStoreLocation Cert:\\LocalMachine\\My -Password $pwd -Exportable -ErrorAction Stop >> "%PSFILE%"
    echo $cert = Get-ChildItem Cert:\\LocalMachine\\My | Where-Object { $_.Subject -like '*CN=SUS.EOS.NeoWallet*' } | Select-Object -First 1 >> "%PSFILE%"
    echo Export-Certificate -Cert $cert -FilePath '%ROOT%NeoWallet-Dev.cer' -Force >> "%PSFILE%"
    echo Import-Certificate -FilePath '%ROOT%NeoWallet-Dev.cer' -CertStoreLocation Cert:\\LocalMachine\\Root -ErrorAction Stop >> "%PSFILE%"
    echo Write-Host 'Certificate imported to LocalMachine\My and trusted in LocalMachine\Root' >> "%PSFILE%"

    echo Running elevated PowerShell to trust the certificate...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File "%PSFILE%"' -Verb RunAs"

    echo If the elevated import succeeded, rerun the MSIX install. Exiting.
    exit /b 0
)


REM ==========================================================
REM SIMPLIFIED CERTIFICATE SETUP
REM ==========================================================

echo.
echo Setting up MSIX code-signing certificate...

if not exist "%CERT_PFX%" (
    echo Creating MSIX certificate...
    powershell -NoProfile -ExecutionPolicy Bypass -Command " $cert = New-SelfSignedCertificate -Subject 'CN=SUS.EOS.NeoWallet' -Type CodeSigningCert -CertStoreLocation Cert:\\CurrentUser\\My -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(3); $pwd = ConvertTo-SecureString -String '%CERT_PASSWORD%' -Force -AsPlainText; Export-PfxCertificate -Cert $cert -FilePath '%CERT_PFX%' -Password $pwd; Import-PfxCertificate -FilePath '%CERT_PFX%' -CertStoreLocation Cert:\\CurrentUser\\My -Password $pwd -Exportable; Write-Host 'Certificate created and imported: ' $cert.Thumbprint "
    for /f %%t in ('powershell -NoProfile -Command "(Get-ChildItem Cert:\\CurrentUser\\My | Where-Object { $_.Subject -like '*CN=SUS.EOS.NeoWallet*' } | Select-Object -First 1 -ExpandProperty Thumbprint).Trim()"') do set CERT_THUMBPRINT=%%t
    echo Certificate Thumbprint: %CERT_THUMBPRINT%

    REM Export public cert and add to CurrentUser\Root (Trusted Root CA) so the MSIX signature is trusted for this user
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Try { $thumb = '%CERT_THUMBPRINT%'; $cerPath = Join-Path '%ROOT%' 'NeoWallet-Dev.cer'; Export-Certificate -Cert Cert:\\CurrentUser\\My\\$thumb -FilePath $cerPath -Force; Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\\CurrentUser\\Root -Verbose; Write-Host 'Certificate exported and trusted in CurrentUser\\Root.' } Catch { Write-Host 'Failed to trust certificate:' $_.Exception.Message; Exit 1 }"
) else (
    echo Using existing certificate: %CERT_PFX%
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Try { $pfx = Get-PfxCertificate -FilePath '%CERT_PFX%'; if ($pfx.Subject -notlike '*CN=SUS.EOS.NeoWallet*') { Write-Host 'Certificate subject mismatch; regenerating certificate with CN=SUS.EOS.NeoWallet...'; $cert = New-SelfSignedCertificate -Subject 'CN=SUS.EOS.NeoWallet' -Type CodeSigningCert -CertStoreLocation Cert:\CurrentUser\My -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(3); $pwd = ConvertTo-SecureString -String '%CERT_PASSWORD%' -Force -AsPlainText; Export-PfxCertificate -Cert $cert -FilePath '%CERT_PFX%' -Password $pwd; Import-PfxCertificate -FilePath '%CERT_PFX%' -CertStoreLocation Cert:\CurrentUser\My -Password $pwd -Exportable; Write-Host 'Recreated and imported certificate.' } else { $pwd = ConvertTo-SecureString -String '%CERT_PASSWORD%' -Force -AsPlainText; Import-PfxCertificate -FilePath '%CERT_PFX%' -CertStoreLocation Cert:\CurrentUser\My -Password $pwd -Exportable; Write-Host 'Existing certificate imported.' } } Catch { Write-Host 'Certificate check/import failed:' $_.Exception.Message; Exit 1 }"
    for /f %%t in ('powershell -NoProfile -Command "(Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*CN=SUS.EOS.NeoWallet*' } | Select-Object -First 1 -ExpandProperty Thumbprint).Trim()"') do set CERT_THUMBPRINT=%%t
    echo Certificate Thumbprint: %CERT_THUMBPRINT%
)

REM ==========================================================
REM RESTORE
REM ==========================================================

echo.
echo ==========================================================
echo Restoring NuGet packages...
echo ==========================================================

dotnet restore "%SOLUTION%" --verbosity minimal > "%LOGS%\restore-%TS%.log" 2>&1

if errorlevel 1 (
    echo ERROR: Restore failed!
    echo Check log: %LOGS%\restore-%TS%.log
    
    REM Try restoring individual projects
    echo Trying to restore Windows project...
    dotnet restore "%WINDOWS_PROJECT%" --verbosity minimal > "%LOGS%\restore-win-%TS%.log" 2>&1
    
    echo Trying to restore Android project...
    dotnet restore "%ANDROID_PROJECT%" --verbosity minimal > "%LOGS%\restore-android-%TS%.log" 2>&1
    
    echo.
    echo Check log files in: %LOGS%\
    exit /b 1
)

echo SUCCESS: NuGet packages restored

REM ==========================================================
REM BUILD SOLUTION
REM ==========================================================

echo.
echo ==========================================================
echo Building solution...
echo ==========================================================

dotnet build "%SOLUTION%" -c %CONFIGURATION% --no-restore > "%LOGS%\build-%TS%.log" 2>&1

if errorlevel 1 (
    echo ERROR: Build failed!
    type "%LOGS%\build-%TS%.log" | findstr /i "error"
    exit /b 1
)

echo SUCCESS: Solution built successfully

REM ==========================================================
REM WINDOWS MSIX PUBLISH (.NET 10)
REM ==========================================================

echo.
echo ==========================================================
echo Building Windows MSIX packages (.NET 10)...
echo ==========================================================

REM Set certificate parameters
if defined CERT_THUMBPRINT (
    set CERT_PARAMS=/p:PackageCertificateThumbprint=%CERT_THUMBPRINT%
) else if exist "%CERT_PFX%" (
    set CERT_PARAMS=/p:PackageCertificateKeyFile="%CERT_PFX%" /p:PackageCertificatePassword=%CERT_PASSWORD%
) else (
    set CERT_PARAMS=/p:AppxPackageSigningEnabled=false
    echo WARNING: No certificate found, MSIX packages will not be signed
)

REM Determine correct TFM for .NET 10
set TFM_WINDOWS=net10.0-windows10.0.19041.0
echo Using TFM: %TFM_WINDOWS%

for %%R in (win-x64 win-x86 win-arm64) do (
    echo.
    echo --- Building for %%R ---
    rem Use a per-run unique output directory to avoid locking collisions
    set OUTDIR=%ARTIFACTS%\windows\%%R\%TS%

    if exist "!OUTDIR!" (
        rmdir /s /q "!OUTDIR!" 2>nul || (
            echo WARNING: Could not remove existing output dir !OUTDIR! - it may be locked; using unique timestamped folder
        )
    )
    mkdir "!OUTDIR!" 2>nul
    
    echo Output directory: !OUTDIR!
    echo Runtime: %%R
    
    echo Publishing Windows app...
    
    REM Publish with ReadyToRun disabled - NO COMMENT ON SAME LINE
    dotnet publish "%WINDOWS_PROJECT%" ^
        -c %CONFIGURATION% ^
        -f %TFM_WINDOWS% ^
        -r %%R ^
        /p:GenerateAppxPackageOnBuild=true ^
        /p:AppxPackageSigningEnabled=true ^
        /p:PublishReadyToRun=false ^
        /p:SelfContained=true ^
        !CERT_PARAMS! ^
        /p:AppxPackageDir="!OUTDIR!" ^
        -o "!OUTDIR!\publish" ^
        > "%LOGS%\publish-win-%%R-%TS%.log" 2>&1
    
    if errorlevel 1 (
        echo ERROR: Windows %%R publish failed
        echo.
        echo Trying alternative publish method without SelfContained...
        
        dotnet publish "%WINDOWS_PROJECT%" ^
            -c %CONFIGURATION% ^
            -f %TFM_WINDOWS% ^
            -r %%R ^
            /p:GenerateAppxPackageOnBuild=true ^
            /p:AppxPackageSigningEnabled=true ^
            /p:PublishReadyToRun=false ^
            /p:SelfContained=false ^
            !CERT_PARAMS! ^
            /p:AppxPackageDir="!OUTDIR!" ^
            -o "!OUTDIR!\publish" ^
            > "%LOGS%\publish-win-%%R-alt-%TS%.log" 2>&1
        
        if errorlevel 1 (
            echo ERROR: Alternative publish also failed for %%R
            echo Check log: %LOGS%\publish-win-%%R-%TS%.log
            exit /b 1
        )
    )
    
    echo SUCCESS: Windows %%R MSIX created
    if exist "!OUTDIR!\*.msix" (
        dir "!OUTDIR!\*.msix" /b
    ) else if exist "!OUTDIR!\*.msixbundle" (
        dir "!OUTDIR!\*.msixbundle" /b
    )
)

REM ==========================================================
REM ANDROID PUBLISH (.NET 10)
REM ==========================================================

echo.
echo ==========================================================
echo Building Android packages (.NET 10)...
echo ==========================================================

REM Check Android SDK
echo Checking Android SDK...
where adb >nul 2>&1
if errorlevel 1 (
    echo WARNING: Android SDK not found in PATH
)

REM Determine Android TFM
set TFM_ANDROID=net10.0-android
echo Using TFM: %TFM_ANDROID%

REM Build APK
echo.
echo --- Building APK ---
set APKOUT=%ARTIFACTS%\android\apk
if exist "%APKOUT%" rmdir /s /q "%APKOUT%"
mkdir "%APKOUT%" 2>nul

dotnet publish "%ANDROID_PROJECT%" ^
    -c %CONFIGURATION% ^
    -f %TFM_ANDROID% ^
    /p:AndroidPackageFormat=apk ^
    /p:AndroidSigningKeyStore="%ROOT%android.keystore" ^
    /p:AndroidSigningStorePass=android ^
    /p:AndroidSigningKeyAlias=androidkey ^
    /p:AndroidSigningKeyPass=android ^
    -o "%APKOUT%" ^
    > "%LOGS%\publish-android-apk-%TS%.log" 2>&1

if errorlevel 1 (
    echo ERROR: Android APK build failed
    
    REM Try without signing
    echo Trying without signing...
    dotnet publish "%ANDROID_PROJECT%" ^
        -c %CONFIGURATION% ^
        -f %TFM_ANDROID% ^
        /p:AndroidPackageFormat=apk ^
        -o "%APKOUT%" ^
        > "%LOGS%\publish-android-apk-nosign-%TS%.log" 2>&1
    
    if errorlevel 1 (
        echo ERROR: Android APK build failed even without signing
        exit /b 1
    )
)

echo SUCCESS: Android APK created
dir "%APKOUT%\*.apk" /b 2>nul

REM Build AAB
echo.
echo --- Building AAB ---
set AABOUT=%ARTIFACTS%\android\aab
if exist "%AABOUT%" rmdir /s /q "%AABOUT%"
mkdir "%AABOUT%" 2>nul

dotnet publish "%ANDROID_PROJECT%" ^
    -c %CONFIGURATION% ^
    -f %TFM_ANDROID% ^
    /p:AndroidPackageFormat=aab ^
    /p:AndroidSigningKeyStore="%ROOT%android.keystore" ^
    /p:AndroidSigningStorePass=android ^
    /p:AndroidSigningKeyAlias=androidkey ^
    /p:AndroidSigningKeyPass=android ^
    -o "%AABOUT%" ^
    > "%LOGS%\publish-android-aab-%TS%.log" 2>&1

if errorlevel 1 (
    echo ERROR: Android AAB build failed
    
    REM Try without signing
    echo Trying without signing...
    dotnet publish "%ANDROID_PROJECT%" ^
        -c %CONFIGURATION% ^
        -f %TFM_ANDROID% ^
        /p:AndroidPackageFormat=aab ^
        -o "%AABOUT%" ^
        > "%LOGS%\publish-android-aab-nosign-%TS%.log" 2>&1
    
    if errorlevel 1 (
        echo ERROR: Android AAB build failed even without signing
        exit /b 1
    )
)

echo SUCCESS: Android AAB created
dir "%AABOUT%\*.aab" /b 2>nul

REM ==========================================================
REM SUMMARY
REM ==========================================================

echo.
echo ==========================================================
echo  BUILD COMPLETED SUCCESSFULLY
echo ==========================================================
echo.
echo .NET Version: %DOTNET_VERSION%
echo Configuration: %CONFIGURATION%
echo.
echo Artifacts created in: %ARTIFACTS%
echo.
echo Windows MSIX packages:
for /f "delims=" %%F in ('dir "%ARTIFACTS%\windows\*.msix" /s /b 2^>nul') do (
    echo  %%F
)
for /f "delims=" %%F in ('dir "%ARTIFACTS%\windows\*.msixbundle" /s /b 2^>nul') do (
    echo  %%F
)
rem If no msix found, warn the user
set MSIXFOUND=
for /f "delims=" %%_ in ('dir "%ARTIFACTS%\windows\*.msix" /s /b 2^>nul ^| findstr /r /c:.') do set MSIXFOUND=1
if not defined MSIXFOUND (
    for /f "delims=" %%_ in ('dir "%ARTIFACTS%\windows\*.msixbundle" /s /b 2^>nul ^| findstr /r /c:.') do set MSIXFOUND=1
)
if not defined MSIXFOUND (
    echo WARNING: No MSIX packages were found in the output directories!
    echo Check the logs in %LOGS% for errors or warnings.
)
echo.
echo Android packages:
dir "%ARTIFACTS%\android\*" /s /b | findstr /i "\.apk$ \.aab$" 2>nul || echo No Android packages found
echo.
echo Logs: %LOGS%
echo.
echo ==========================================================

exit /b 0