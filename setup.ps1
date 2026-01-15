param(
    [string] $DotnetChannel = "LTS",
    [string] $PlaywrightBrowsers = "chromium",
    [string] $PlaywrightScriptPath = "",
    [switch] $ForceDotnetInstall
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step([string] $Message) {
    Write-Host ("[SETUP] " + $Message) -ForegroundColor Cyan
}

function Write-Ok([string] $Message) {
    Write-Host ("[ OK ] " + $Message) -ForegroundColor Green
}

function Write-Warn([string] $Message) {
    Write-Warning ("[WARN] " + $Message)
}

function Ensure-Tls12 {
    # Windows PowerShell 5.1 下默认可能不是 TLS 1.2，拉取 https 资源会失败
    try {
        if ($PSVersionTable.PSEdition -ne "Core") {
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        }
    } catch {
        Write-Warn "无法设置 TLS 1.2（可能不影响）。"
    }
}

function Add-ToPathOnce([string] $Dir) {
    if ([string]::IsNullOrWhiteSpace($Dir)) { return }
    if (-not (Test-Path -LiteralPath $Dir)) { return }
    $pathParts = $env:Path -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($pathParts -notcontains $Dir) {
        $env:Path = "$Dir;" + $env:Path
    }
}

function Test-DotnetAvailable {
    return [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
}

try {
    Write-Step "开始环境配置（PowerShell $($PSVersionTable.PSVersion)，时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')）"

    if ($env:OS -ne 'Windows_NT') {
        Write-Warn "当前不是 Windows 环境；脚本主要面向 Windows。"
    }

    Ensure-Tls12

    # 1) 安装/确认 .NET SDK
    $dotnetDir = Join-Path $env:LocalAppData "Microsoft\dotnet"
    $dotnetToolsDir = Join-Path $env:UserProfile ".dotnet\tools"

    Add-ToPathOnce $dotnetDir
    Add-ToPathOnce $dotnetToolsDir

    $dotnetExists = Test-DotnetAvailable
    if ($dotnetExists -and -not $ForceDotnetInstall) {
        Write-Ok ".NET 已检测到：$((dotnet --version) 2>$null)"
    } else {
        Write-Step "正在安装/更新 .NET SDK（Channel=$DotnetChannel，InstallDir=$dotnetDir）"
        $dotnetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
        $dotnetInstallScript = Join-Path $env:TEMP "dotnet-install.ps1"

        Write-Step "下载 dotnet-install.ps1：$dotnetInstallUrl"
        if ($PSVersionTable.PSEdition -ne "Core") {
            Invoke-WebRequest -Uri $dotnetInstallUrl -OutFile $dotnetInstallScript -UseBasicParsing
        } else {
            Invoke-WebRequest -Uri $dotnetInstallUrl -OutFile $dotnetInstallScript
        }

        Write-Step "执行 dotnet-install.ps1（这一步可能需要几分钟）"
        & $dotnetInstallScript -Channel $DotnetChannel -InstallDir $dotnetDir

        Add-ToPathOnce $dotnetDir
        Add-ToPathOnce $dotnetToolsDir

        if (-not (Test-DotnetAvailable)) {
            throw "dotnet 安装后仍不可用。请重开一个 PowerShell，再运行一次脚本，或检查执行策略/杀毒拦截。"
        }

        Write-Ok ".NET 安装完成：$((dotnet --version) 2>$null)"
    }

    Write-Step "dotnet --info（用于确认环境）"
    dotnet --info | Out-Host

    # 2) 安装浏览器（如已存在则跳过），使用本地 playwright.ps1 脚本
    $msPwDir = Join-Path $env:LocalAppData 'ms-playwright'
    $browserNames = $PlaywrightBrowsers -split '\s+' | Where-Object { $_ -and $_.Trim().Length -gt 0 }
    $needInstall = $false
    foreach ($b in $browserNames) {
        $exists = $false
        if (Test-Path -LiteralPath $msPwDir) {
            $exists = @(Get-ChildItem -Path $msPwDir -Directory | Where-Object { $_.Name -like ("$b*") }).Count -gt 0
        }
        if ($exists) {
            Write-Ok "检测到已安装浏览器：$b，跳过安装"
        } else {
            $needInstall = $true
        }
    }

    if ($needInstall) {
        # 解析 playwright.ps1 路径
        $resolvedPlaywright = $PlaywrightScriptPath
        if ([string]::IsNullOrWhiteSpace($resolvedPlaywright)) {
            $candidates = @(
                (Join-Path $PSScriptRoot 'playwright.ps1'),
                (Join-Path $PSScriptRoot 'XiaoheiheMcpServer\bin\Debug\net10.0\playwright.ps1'),
                (Join-Path $PSScriptRoot 'XiaoheiheMcpServer\bin\Release\net10.0\playwright.ps1'),
                (Join-Path $PSScriptRoot 'XiaoheiheMcpServer\bin\Release\net10.0\publish\playwright.ps1')
            )
            foreach ($p in $candidates) {
                if (Test-Path -LiteralPath $p) { $resolvedPlaywright = $p; break }
            }
        }

        if (-not (Test-Path -LiteralPath $resolvedPlaywright)) {
            throw "未找到 playwright.ps1 脚本。请确保它位于项目根或提供 -PlaywrightScriptPath。"
        }

        Write-Step "使用脚本安装 Playwright 浏览器：$PlaywrightBrowsers（脚本：$resolvedPlaywright）"
        & $resolvedPlaywright install $PlaywrightBrowsers | Out-Host
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Playwright 浏览器安装完成"
        } else {
            Write-Warn "Playwright 浏览器安装脚本返回非零退出码，请检查日志或网络后重试。"
        }
    }

    Write-Ok "环境配置完成。"
} catch {
    Write-Host "[FAIL] 环境配置失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "[FAIL] 详情：$($_ | Out-String)" -ForegroundColor DarkRed
    exit 1
}