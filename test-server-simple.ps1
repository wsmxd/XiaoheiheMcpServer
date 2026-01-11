#!/usr/bin/env pwsh
# 简单的服务器启动测试

Write-Host "简单启动测试 - 小黑盒MCP服务器" -ForegroundColor Green
Write-Host "按 Ctrl+C 停止服务器`n" -ForegroundColor Yellow

Set-Location XiaoheiheMcpServer

Write-Host "构建项目..." -ForegroundColor Cyan
dotnet build --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n启动服务器 (有界面模式用于调试)..." -ForegroundColor Green
    Write-Host "服务器已启动，等待 MCP 客户端连接..." -ForegroundColor Cyan
    Write-Host "日志输出:" -ForegroundColor Yellow
    
    # 启动服务器并显示所有输出
    dotnet run -- --no-headless
} else {
    Write-Host "构建失败!" -ForegroundColor Red
}

Set-Location ..
