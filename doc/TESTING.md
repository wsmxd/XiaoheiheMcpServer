# 测试小黑盒 MCP 服务器

本目录包含用于测试 MCP 服务器的脚本和说明。

## 测试方法

### 方法 1: 使用 MCP Inspector（推荐）

MCP Inspector 是官方的测试工具，提供可视化界面。

1. 安装 Node.js (如果尚未安装)

2. 启动 Inspector:
```bash
npx @modelcontextprotocol/inspector dotnet run --project XiaoheiheMcpServer/XiaoheiheMcpServer.csproj
```

3. 在浏览器中打开显示的 URL (通常是 http://localhost:5173)

4. 在 Inspector 界面中可以：
   - 查看所有可用工具
   - 测试每个工具的调用
   - 查看请求和响应
   - 查看服务器日志

### 方法 2: 在 Claude Desktop 中测试

1. 编辑 Claude Desktop 配置文件：
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

2. 添加配置：
```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:/vs/.NETWithAI/XiaoheiheMcpServer/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"
      ]
    }
  }
}
```

3. 重启 Claude Desktop

4. 在对话中测试：
   - "帮我检查小黑盒登录状态"
   - "获取小黑盒登录二维码"
   - 等等

### 方法 3: 使用命令行测试（调试用）

运行简单的启动测试：
```bash
.\test-server-simple.ps1
```

这将以有界面模式启动服务器，你可以看到浏览器窗口和详细日志。

### 方法 4: 手动 stdin/stdout 测试

对于高级用户，可以手动发送 JSON-RPC 消息：

1. 启动服务器:
```bash
cd XiaoheiheMcpServer
dotnet run
```

2. 在 stdin 中输入 JSON-RPC 消息（每行一个）：

初始化:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
```

列出工具:
```json
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
```

## 预期的工具列表

服务器应该提供以下 6 个工具：

1. `check_login_status` - 检查登录状态
2. `get_login_qrcode` - 获取登录二维码
3. `publish_content` - 发布图文内容
4. `search_content` - 搜索内容
5. `get_post_detail` - 获取帖子详情
6. `post_comment` - 发表评论

## 故障排查

### 服务器立即退出
- 这是正常的！MCP 服务器需要客户端持续连接
- 使用 Inspector 或 Claude Desktop 来保持连接

### 找不到 Chromium
```bash
cd XiaoheiheMcpServer
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### 连接超时
- 检查防火墙设置
- 确认项目路径正确
- 查看服务器日志（stderr）

## 单元测试

运行自动化单元测试：
```bash
dotnet test
```

这将运行 14 个单元测试（6 个服务测试 + 8 个模型测试），不需要真实的浏览器环境。
