@echo off
chcp 65001 >nul
echo ========================================
echo    Duckov Mod Environment Setup
echo ========================================
echo.

echo This script will set DUCKOV_GAME_MANAGED environment variable for current session only
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
echo Setting variable for current session:
echo   DUCKOV_GAME_MANAGED=%DUCKOV_GAME_MANAGED%
echo.

echo ========================================
echo Environment variable set successfully!
echo ========================================
echo.
echo NOTE: This variable is only valid in current window.
echo For permanent setup, run SetEnvVars_Permanent.bat
echo.
echo You can now compile the project in this window.
echo.

pause
