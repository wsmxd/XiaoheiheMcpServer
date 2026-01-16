@echo off
setlocal

REM Installs Playwright browser (default chromium). Skips if already present in cache.
REM Usage: install-chromium.bat [chromium|firefox|webkit]

set "BROWSER=%~1"
if "%BROWSER%"=="" set "BROWSER=chromium"

REM Determine cache root
if not "%PLAYWRIGHT_BROWSERS_PATH%"=="" (
    set "BROWSERS_PATH=%PLAYWRIGHT_BROWSERS_PATH%"
) else (
    set "BROWSERS_PATH=%LOCALAPPDATA%\ms-playwright"
)

REM Check existing installation
if exist "%BROWSERS_PATH%\%BROWSER%-*" (
    echo Playwright browser already installed: %BROWSER%
    goto :done
)

set "PW_SCRIPT=%~dp0playwright.ps1"
if not exist "%PW_SCRIPT%" (
    echo playwright.ps1 not found next to this script.
    exit /b 1
)

echo Installing browser via Playwright: %BROWSER%
powershell -ExecutionPolicy Bypass -File "%PW_SCRIPT%" install %BROWSER%

:done
pause
endlocal
