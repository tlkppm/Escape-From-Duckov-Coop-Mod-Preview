@echo off
chcp 65001 >nul

REM Check admin rights and auto-elevate if needed
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting administrator privileges...
    echo.
    
    REM Use PowerShell to elevate
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ========================================
echo Duckov Mod Environment Setup (Permanent)
echo ========================================
echo.

echo This script will permanently set DUCKOV_GAME_MANAGED user environment variable.
echo.
echo Please enter the full path to your game's Managed folder.
echo Example: C:\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Managed
echo.

set /p "DUCKOV_GAME_MANAGED=Enter path: "

:SET_ENV
echo.
echo ========================================
echo Setting environment variable...
echo ========================================

REM Check if path exists
echo Validating path...
if not exist "%DUCKOV_GAME_MANAGED%" (
    echo [WARNING] Path not found: %DUCKOV_GAME_MANAGED%
    echo.
)

echo.
echo Setting permanent user environment variable...

REM Use setx to set user environment variable
setx DUCKOV_GAME_MANAGED "%DUCKOV_GAME_MANAGED%" >nul
if %errorLevel% equ 0 (
    echo [SUCCESS] DUCKOV_GAME_MANAGED set successfully
) else (
    echo [FAILED] DUCKOV_GAME_MANAGED failed to set
)

echo.
echo ========================================
echo Environment variable set successfully!
echo ========================================
echo.
echo Variable set:
echo   DUCKOV_GAME_MANAGED=%DUCKOV_GAME_MANAGED%
echo.
echo IMPORTANT:
echo 1. Restart any open programs to load new environment variable
echo 2. Visual Studio must be completely closed and reopened
echo 3. Command windows must be closed and reopened
echo.

pause
