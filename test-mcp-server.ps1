#!/usr/bin/env pwsh
# 测试MCP服务器的启动和基本通信

Write-Host "测试小黑盒MCP服务器..." -ForegroundColor Green

# 构建服务器
Write-Host "`n1. 构建服务器..." -ForegroundColor Yellow
Set-Location XiaoheiheMcpServer
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败!" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. 启动服务器并测试基本通信..." -ForegroundColor Yellow

# 创建测试消息
$initMessage = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "test-client"
            version = "1.0.0"
        }
    }
} | ConvertTo-Json -Compress

$listToolsMessage = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
    params = @{}
} | ConvertTo-Json -Compress

# 使用临时文件存储输入
$tempInput = New-TemporaryFile
@"
$initMessage
$listToolsMessage
"@ | Out-File -FilePath $tempInput.FullName -Encoding UTF8

Write-Host "发送初始化消息..." -ForegroundColor Cyan

# 启动服务器进程（5秒超时）
$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -NoNewWindow -PassThru -RedirectStandardInput $tempInput.FullName -RedirectStandardOutput "test-output.json" -RedirectStandardError "test-error.log"

# 等待5秒让服务器处理消息
Start-Sleep -Seconds 5

# 终止进程
Stop-Process -Id $process.Id -Force

# 读取输出
if (Test-Path "test-output.json") {
    Write-Host "`n3. 服务器响应:" -ForegroundColor Green
    $output = Get-Content "test-output.json" -Raw
    $output | Write-Host
    
    # 清理
    Remove-Item "test-output.json" -Force
} else {
    Write-Host "未收到服务器响应" -ForegroundColor Red
}

if (Test-Path "test-error.log") {
    Write-Host "`n4. 服务器日志:" -ForegroundColor Yellow
    Get-Content "test-error.log" | Write-Host
    Remove-Item "test-error.log" -Force
}

# 清理临时文件
Remove-Item $tempInput.FullName -Force

Write-Host "`n测试完成!" -ForegroundColor Green

Set-Location ..
