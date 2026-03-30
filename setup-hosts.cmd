@echo off
:: Neo Wallet - Hostname Setup
:: Adds neo.wallet to the system hosts file so the wallet is accessible
:: at http://neo.wallet:5199 instead of http://localhost:5199.
:: This only needs to be run once.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo === Neo Wallet Hostname Setup ===
echo.

set "HOSTS=%SystemRoot%\System32\drivers\etc\hosts"

findstr /i "neo.wallet" "%HOSTS%" >nul 2>&1
if %errorlevel% equ 0 (
    echo neo.wallet is already configured in your hosts file.
) else (
    echo Adding 127.0.0.1 neo.wallet to hosts file...
    echo.>> "%HOSTS%"
    echo 127.0.0.1 neo.wallet>> "%HOSTS%"
    if %errorlevel% equ 0 (
        echo Done! neo.wallet hostname configured.
    ) else (
        echo Failed to modify hosts file.
    )
)

echo.
pause
