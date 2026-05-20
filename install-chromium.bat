@echo off
setlocal

REM Installs Playwright browser (default chromium). Skips if Chromium/Chrome is new enough.
REM Usage: install-chromium.bat [chromium|firefox|webkit] [minimum-chromium-major-version]

set "BROWSER=%~1"
if "%BROWSER%"=="" set "BROWSER=chromium"
set "MIN_CHROMIUM_VERSION=%~2"
if "%MIN_CHROMIUM_VERSION%"=="" set "MIN_CHROMIUM_VERSION=120"
set "EXIT_CODE=0"

set "PW_SCRIPT=%~dp0playwright.ps1"
if not exist "%PW_SCRIPT%" (
    echo playwright.ps1 not found next to this script.
    exit /b 1
)

set "SETUP_SCRIPT=%~dp0setup.ps1"
if exist "%SETUP_SCRIPT%" (
    echo Checking/installing browser via setup.ps1: %BROWSER%
    powershell -ExecutionPolicy Bypass -File "%SETUP_SCRIPT%" -PlaywrightBrowsers "%BROWSER%" -PlaywrightScriptPath "%PW_SCRIPT%" -MinimumChromiumMajorVersion %MIN_CHROMIUM_VERSION%
    set "EXIT_CODE=%ERRORLEVEL%"
    goto :done
)

echo Installing browser via Playwright: %BROWSER%
powershell -ExecutionPolicy Bypass -File "%PW_SCRIPT%" install %BROWSER%
set "EXIT_CODE=%ERRORLEVEL%"

:done
pause
exit /b %EXIT_CODE%
