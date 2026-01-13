#!/usr/bin/env pwsh
# Xiaoheihei MCP Server 初始化脚本
# 用途：检查和安装必要的依赖（.NET 运行时和 Playwright）

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "小黑盒 MCP Server 初始化脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 .NET 运行时
Write-Host "检查 .NET 10.0 运行时..." -ForegroundColor Yellow

try {
    $dotnetOutput = dotnet --version 2>$null
    $dotnetVersion = $dotnetOutput.Split('.')[0]
    
    if ($dotnetVersion -ge 10) {
        Write-Host "✅ 已安装 .NET $dotnetOutput" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  当前 .NET 版本为 $dotnetVersion，需要 .NET 10.0 或更高版本" -ForegroundColor Yellow
        throw "需要升级 .NET"
    }
}
catch {
    Write-Host "❌ 未检测到 .NET 运行时" -ForegroundColor Red
    Write-Host ""
    Write-Host "请访问以下链接下载 .NET 10.0:" -ForegroundColor Yellow
    Write-Host "https://dotnet.microsoft.com/en-us/download/dotnet/10.0" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "或者运行以下命令安装（需要管理员权限）:" -ForegroundColor Yellow
    Write-Host "winget install Microsoft.DotNet.Runtime.10" -ForegroundColor Cyan
    Write-Host ""
    Exit 1
}

# 2. 检查和安装 Playwright
Write-Host ""
Write-Host "检查 Playwright 浏览器..." -ForegroundColor Yellow

$playwrightPath = "$env:APPDATA\ms-playwright"

if (Test-Path $playwrightPath) {
    Write-Host "✅ Playwright 已安装" -ForegroundColor Green
}
else {
    Write-Host "🔄 首次运行需要安装 Playwright 浏览器..." -ForegroundColor Yellow
    Write-Host "这可能需要几分钟时间，请耐心等待..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # 使用 dotnet 工具安装 Playwright
        $env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = $null
        dotnet tool install -g Microsoft.Playwright.CLI --version 1.57.0 2>$null
        
        if (($LASTEXITCODE -eq 0) -or ($(playwright --version 2>$null).Count -gt 0)) {
            Write-Host "运行 Playwright 安装..." -ForegroundColor Yellow
            playwright install chromium
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Playwright 安装成功" -ForegroundColor Green
            }
            else {
                Write-Host "⚠️  Playwright 安装可能存在问题，但服务器将在首次运行时尝试安装" -ForegroundColor Yellow
            }
        }
    }
    catch {
        Write-Host "⚠️  自动安装 Playwright 失败，服务器将在首次运行时尝试安装" -ForegroundColor Yellow
        Write-Host "错误信息: $_" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "初始化完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "现在可以运行服务器了：" -ForegroundColor Cyan
Write-Host "  ./XiaoheiheMcpServer.exe" -ForegroundColor Yellow
Write-Host ""
Write-Host "有头模式（推荐首次登录）：" -ForegroundColor Cyan
Write-Host "  ./XiaoheiheMcpServer.exe --no-headless" -ForegroundColor Yellow
Write-Host ""
