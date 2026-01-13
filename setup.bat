@echo off
REM Xiaoheihei MCP Server 初始化脚本（Windows 批处理版本）
REM 用途：检查和安装必要的依赖（.NET 运行时和 Playwright）

setlocal enabledelayedexpansion
chcp 65001 >nul

echo.
echo ========================================
echo 小黑盒 MCP Server 初始化脚本
echo ========================================
echo.

REM 1. 检查 .NET 运行时
echo 检查 .NET 10.0 运行时...

dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo ❌ 未检测到 .NET 运行时
    echo.
    echo 请访问以下链接下载 .NET 10.0:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    echo.
    echo 或者运行以下命令安装（需要管理员权限）:
    echo winget install Microsoft.DotNet.Runtime.10
    echo.
    pause
    exit /b 1
)

for /f "tokens=1" %%i in ('dotnet --version') do (
    set DOTNET_VERSION=%%i
)
echo ✅ 已安装 .NET !DOTNET_VERSION!
echo.

REM 2. 检查和安装 Playwright
echo 检查 Playwright 浏览器...

if exist "%APPDATA%\ms-playwright" (
    echo ✅ Playwright 已安装
) else (
    echo 🔄 首次运行需要安装 Playwright 浏览器...
    echo 这可能需要几分钟时间，请耐心等待...
    echo.
    
    REM 尝试使用 dotnet tool 安装
    dotnet tool install -g Microsoft.Playwright.CLI --version 1.57.0 >nul 2>&1
    
    if %errorlevel% equ 0 (
        echo 运行 Playwright 安装...
        playwright install chromium
        
        if %errorlevel% equ 0 (
            echo ✅ Playwright 安装成功
        ) else (
            echo ⚠️  Playwright 安装可能存在问题，但服务器将在首次运行时尝试安装
        )
    ) else (
        echo ⚠️  自动安装 Playwright 失败，服务器将在首次运行时尝试安装
    )
)

echo.
echo ========================================
echo ✅ 初始化完成！
echo ========================================
echo.
echo 现在可以运行服务器了：
echo   XiaoheiheMcpServer.exe
echo.
echo 有头模式（推荐首次登录）：
echo   XiaoheiheMcpServer.exe --no-headless
echo.
pause
