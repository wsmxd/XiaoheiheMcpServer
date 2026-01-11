#!/usr/bin/env pwsh
# 测试MCP服务器的启动和基本通信

Write-Host "测试小黑盒MCP服务器..." -ForegroundColor Green

# 构建服务器
Write-Host "`n1. 构建服务器..." -ForegroundColor Yellow
Set-Location XiaoheiheMcpServer
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败!" -ForegroundColor Red
    Set-Location ..
    exit 1
}

Write-Host "`n2. 测试服务器启动..." -ForegroundColor Yellow

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
} | ConvertTo-Json -Compress -Depth 10

$listToolsMessage = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
    params = @{}
} | ConvertTo-Json -Compress

Write-Host "发送测试消息到服务器..." -ForegroundColor Cyan

# 使用管道方式与服务器通信
try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = "run --no-build"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    
    # 捕获输出
    $outputBuilder = New-Object System.Text.StringBuilder
    $errorBuilder = New-Object System.Text.StringBuilder
    
    $outputEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if ($EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $outputBuilder

    $errorEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if ($EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $errorBuilder

    Write-Host "启动服务器进程..." -ForegroundColor Cyan
    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    # 等待服务器初始化
    Start-Sleep -Seconds 2

    # 发送初始化消息
    Write-Host "发送 initialize 请求..." -ForegroundColor Cyan
    $process.StandardInput.WriteLine($initMessage)
    $process.StandardInput.Flush()
    
    Start-Sleep -Seconds 2

    # 发送工具列表请求
    Write-Host "发送 tools/list 请求..." -ForegroundColor Cyan
    $process.StandardInput.WriteLine($listToolsMessage)
    $process.StandardInput.Flush()

    # 等待响应
    Start-Sleep -Seconds 3

    # 关闭输入流
    $process.StandardInput.Close()

    # 等待进程退出或超时
    $process.WaitForExit(2000) | Out-Null

    # 停止进程
    if (!$process.HasExited) {
        Write-Host "正常关闭服务器..." -ForegroundColor Cyan
        $process.Kill()
        $process.WaitForExit(1000) | Out-Null
    }

    # 清理事件
    Unregister-Event -SourceIdentifier $outputEvent.Name
    Unregister-Event -SourceIdentifier $errorEvent.Name

    # 显示输出
    Write-Host "`n3. 服务器响应 (stdout):" -ForegroundColor Green
    $output = $outputBuilder.ToString()
    if ($output) {
        # 尝试解析并格式化 JSON 响应
        $lines = $output -split "`n" | Where-Object { $_.Trim() }
        foreach ($line in $lines) {
            try {
                $json = $line | ConvertFrom-Json
                $json | ConvertTo-Json -Depth 10 | Write-Host
            } catch {
                Write-Host $line
            }
        }
    } else {
        Write-Host "  (无输出)" -ForegroundColor Gray
    }

    Write-Host "`n4. 服务器日志 (stderr):" -ForegroundColor Yellow
    $errors = $errorBuilder.ToString()
    if ($errors) {
        $errors | Write-Host
    } else {
        Write-Host "  (无日志)" -ForegroundColor Gray
    }

    $process.Dispose()

} catch {
    Write-Host "`n错误: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($process -and !$process.HasExited) {
        $process.Kill()
    }
} finally {
    Set-Location ..
}

Write-Host "`n测试完成!" -ForegroundColor Green
Write-Host "`n提示: 要在 Claude Desktop 中使用，请配置 claude_desktop_config.json" -ForegroundColor Cyan
