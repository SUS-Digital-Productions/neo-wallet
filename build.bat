@echo off
REM Quick build script wrapper for Windows users
REM Usage: build.bat [Android|Windows|iOS|macOS] [Debug|Release]

setlocal

set PLATFORMS=%1
set CONFIG=%2

if "%PLATFORMS%"=="" set PLATFORMS=Android,Windows
if "%CONFIG%"=="" set CONFIG=Release

echo.
echo ===============================================
echo   SUS.EOS.NeoWallet Multi-Platform Builder
echo ===============================================
echo.
echo Platforms: %PLATFORMS%
echo Configuration: %CONFIG%
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Platforms %PLATFORMS% -Configuration %CONFIG%

if %errorlevel% equ 0 (
    echo.
    echo ===============================================
    echo   Build Completed Successfully!
    echo ===============================================
    echo.
    echo Output location: %~dp0build-output
    echo.
) else (
    echo.
    echo ===============================================
    echo   Build Failed!
    echo ===============================================
    echo.
    echo Check logs in: %~dp0build-output\logs
    echo.
)

pause
