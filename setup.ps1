param(
    [string] $DotnetChannel = "LTS",
    [string] $PlaywrightBrowsers = "chromium",
    [string] $PlaywrightScriptPath = "",
    [int] $MinimumChromiumMajorVersion = 120,
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
    $pathParts = $env:Path -split [IO.Path]::PathSeparator | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($pathParts -notcontains $Dir) {
        $env:Path = "$Dir$([IO.Path]::PathSeparator)$env:Path"
    }
}

function Test-DotnetAvailable {
    return [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
}

function Test-IsWindows {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
}

function Test-IsMacOS {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)
}

function Get-UserHome {
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) { return $env:USERPROFILE }
    return $HOME
}

function Get-DefaultDotnetDir {
    if (Test-IsWindows) {
        return Join-Path $env:LocalAppData 'Microsoft\dotnet'
    }

    return Join-Path (Get-UserHome) '.dotnet'
}

function Get-DotnetToolsDir {
    return Join-Path (Join-Path (Get-UserHome) '.dotnet') 'tools'
}

function Get-DefaultPlaywrightBrowsersPath {
    if (-not [string]::IsNullOrWhiteSpace($env:PLAYWRIGHT_BROWSERS_PATH) -and $env:PLAYWRIGHT_BROWSERS_PATH -ne '0') {
        return $env:PLAYWRIGHT_BROWSERS_PATH
    }

    if (Test-IsWindows) {
        return Join-Path $env:LocalAppData 'ms-playwright'
    }

    if (Test-IsMacOS) {
        return Join-Path (Join-Path (Join-Path (Get-UserHome) 'Library') 'Caches') 'ms-playwright'
    }

    return Join-Path (Join-Path (Get-UserHome) '.cache') 'ms-playwright'
}

function Get-ChromiumVersion([string] $ExecutablePath) {
    if (-not (Test-Path -LiteralPath $ExecutablePath)) { return $null }

    try {
        $output = & $ExecutablePath --version 2>&1 | Out-String
        if ($output -match '\b(\d{2,3})\.\d+\.') {
            return [int] $Matches[1]
        }
    } catch {
        return $null
    }

    return $null
}

function Test-ChromiumExecutable([string] $ExecutablePath, [int] $MinimumVersion) {
    $version = Get-ChromiumVersion $ExecutablePath
    return $null -ne $version -and $version -ge $MinimumVersion
}

function Get-PlaywrightChromiumExecutables([string] $BrowsersRoot) {
    if ([string]::IsNullOrWhiteSpace($BrowsersRoot) -or -not (Test-Path -LiteralPath $BrowsersRoot)) { return @() }

    $dirs = Get-ChildItem -Path $BrowsersRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'chromium-*' } |
        Sort-Object Name -Descending

    $executables = @()
    foreach ($dir in $dirs) {
        if (Test-IsWindows) {
            $executables += Join-Path (Join-Path $dir.FullName 'chrome-win') 'chrome.exe'
        } elseif (Test-IsMacOS) {
            $executables += Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $dir.FullName 'chrome-mac') 'Chromium.app') 'Contents') 'MacOS') 'Chromium'
        } else {
            $executables += Join-Path (Join-Path $dir.FullName 'chrome-linux') 'chrome'
        }
    }

    return $executables
}

function Get-SystemChromiumExecutables {
    $executables = @()

    if (-not [string]::IsNullOrWhiteSpace($env:XIAOHEIHE_CHROMIUM_PATH)) {
        $executables += $env:XIAOHEIHE_CHROMIUM_PATH
    }

    if (Test-IsWindows) {
        $roots = @($env:LocalAppData, $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($root in $roots) {
            $executables += Join-Path (Join-Path (Join-Path $root 'Chromium') 'Application') 'chrome.exe'
            $executables += Join-Path (Join-Path (Join-Path (Join-Path $root 'Google') 'Chrome') 'Application') 'chrome.exe'
            $executables += Join-Path (Join-Path (Join-Path (Join-Path $root 'Microsoft') 'Edge') 'Application') 'msedge.exe'
        }
    } elseif (Test-IsMacOS) {
        $executables += '/Applications/Chromium.app/Contents/MacOS/Chromium'
        $executables += '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
        $executables += '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'
        $executables += (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path (Get-UserHome) 'Applications') 'Chromium.app') 'Contents') 'MacOS') 'Chromium')
        $executables += (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path (Get-UserHome) 'Applications') 'Google Chrome.app') 'Contents') 'MacOS') 'Google Chrome')
        $executables += (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path (Get-UserHome) 'Applications') 'Microsoft Edge.app') 'Contents') 'MacOS') 'Microsoft Edge')
    } else {
        $executables += '/usr/bin/chromium'
        $executables += '/usr/bin/chromium-browser'
        $executables += '/usr/bin/google-chrome'
        $executables += '/usr/bin/google-chrome-stable'
        $executables += '/usr/bin/microsoft-edge'
        $executables += '/usr/bin/microsoft-edge-stable'
        $executables += '/snap/bin/chromium'
    }

    foreach ($name in @('chromium', 'chromium-browser', 'google-chrome', 'google-chrome-stable', 'chrome', 'msedge', 'microsoft-edge', 'microsoft-edge-stable')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { $executables += $command.Source }
    }

    return $executables | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
}

function Test-UsableChromium([int] $MinimumVersion) {
    $candidates = @()
    $candidates += Get-PlaywrightChromiumExecutables (Get-DefaultPlaywrightBrowsersPath)
    $candidates += Get-SystemChromiumExecutables

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-ChromiumExecutable $candidate $MinimumVersion) {
            Write-Ok "检测到可用 Chromium/Chrome：$candidate"
            return $true
        }
    }

    return $false
}

try {
    Write-Step "开始环境配置（PowerShell $($PSVersionTable.PSVersion)，时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')）"

    Ensure-Tls12

    # 1) 安装/确认 .NET SDK
    $dotnetDir = Get-DefaultDotnetDir
    $dotnetToolsDir = Get-DotnetToolsDir

    Add-ToPathOnce $dotnetDir
    Add-ToPathOnce $dotnetToolsDir

    $dotnetExists = Test-DotnetAvailable
    if ($dotnetExists -and -not $ForceDotnetInstall) {
        Write-Ok ".NET 已检测到：$((dotnet --version) 2>$null)"
    } else {
        Write-Step "正在安装/更新 .NET SDK（Channel=$DotnetChannel，InstallDir=$dotnetDir）"
        $dotnetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
        $dotnetInstallScript = Join-Path ([IO.Path]::GetTempPath()) "dotnet-install.ps1"

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

    # 2) 安装浏览器（如已存在且版本满足则跳过），使用本地 playwright.ps1 脚本
    $msPwDir = Get-DefaultPlaywrightBrowsersPath
    $browserNames = $PlaywrightBrowsers -split '\s+' | Where-Object { $_ -and $_.Trim().Length -gt 0 }
    $needInstall = $false
    foreach ($b in $browserNames) {
        if ($b -eq 'chromium' -and (Test-UsableChromium $MinimumChromiumMajorVersion)) {
            Write-Ok "检测到主版本 >= $MinimumChromiumMajorVersion 的 Chromium/Chrome，跳过安装"
        } elseif ($b -ne 'chromium' -and (Test-Path -LiteralPath $msPwDir) -and @(Get-ChildItem -Path $msPwDir -Directory | Where-Object { $_.Name -like ("$b*") }).Count -gt 0) {
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
                (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $PSScriptRoot 'XiaoheiheMcpServer') 'bin') 'Debug') 'net10.0') 'playwright.ps1'),
                (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $PSScriptRoot 'XiaoheiheMcpServer') 'bin') 'Release') 'net10.0') 'playwright.ps1'),
                (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $PSScriptRoot 'XiaoheiheMcpServer') 'bin') 'Release') 'net10.0') 'publish') 'playwright.ps1')
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
