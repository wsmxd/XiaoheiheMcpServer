# 小黑盒 MCP 服务器

使用 .NET 实现的小黑盒社交网站 MCP (Model Context Protocol) 服务器，类似于 xiaohongshu-mcp 项目。

## 功能特性

- ✅ 检查登录状态
- ✅ 二维码登录
- ✅ 发布图文内容
- ✅ 发布评论
- ✅ 搜索内容
- ✅ 获取帖子详情

## 安装步骤

### 1. 安装依赖

```bash
cd XiaoheiheMcpServer
dotnet restore
```

### 2. 安装 Playwright 浏览器

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## 使用方法

### 1. 首次使用 - 登录

首先需要获取登录二维码并扫码登录：

```bash
dotnet run
```

服务启动后，使用 MCP 客户端调用 `get_login_qrcode` 工具获取二维码，使用小黑盒 APP 扫码登录。

### 2. 运行模式

**无头模式（默认）**:
```bash
dotnet run
```

**有界面模式（调试用）**:
```bash
dotnet run -- --no-headless
```

### 3. 配置 MCP 客户端

在你的 MCP 客户端（如 Claude Desktop、Cursor 等）配置文件中添加：

**Claude Desktop (claude_desktop_config.json)**:
```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\vs\\.NETWithAI\\XiaoheiheMcpServer\\XiaoheiheMcpServer"
      ]
    }
  }
}
```

**Cursor (.cursor/mcp.json)**:
```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\vs\\.NETWithAI\\XiaoheiheMcpServer\\XiaoheiheMcpServer"
      ]
    }
  }
}
```

## 可用工具

### 1. check_login_status
检查小黑盒登录状态
- 参数：无
- 返回：登录状态和用户名

### 2. get_login_qrcode
获取登录二维码
- 参数：无
- 返回：二维码图片（Base64）和过期时间

### 3. publish_content
发布图文内容到小黑盒
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `images`: 图片路径列表（可选，本地绝对路径）
  - `tags`: 标签列表（可选）

### 4. search_content
搜索小黑盒内容
- 参数：
  - `keyword`: 搜索关键词（必需）
  - `page`: 页码（可选，默认 1）
  - `pageSize`: 每页数量（可选，默认 20）

### 5. get_post_detail
获取帖子详情
- 参数：
  - `postId`: 帖子ID（必需）

### 6. post_comment
发表评论
- 参数：
  - `postId`: 帖子ID（必需）
  - `content`: 评论内容（必需）

## 使用示例

在 MCP 客户端中：

```
帮我检查小黑盒登录状态
```

```
帮我发布一篇关于游戏的帖子到小黑盒
标题：《原神》新角色体验
内容：今天体验了新角色，感觉非常不错...
图片：C:\Users\用户\Pictures\game.jpg
标签：原神,游戏评测
```

```
搜索小黑盒上关于"原神"的内容
```

## 数据存储

- Cookies 存储在 `data/cookies.json`
- 登录后会自动保存 Cookies，下次启动无需重新登录

## 注意事项

1. 首次运行会下载 Chromium 浏览器（约 150MB）
2. 图片路径需使用本地绝对路径
3. 请根据小黑盒实际页面结构调整选择器
4. 建议使用无头模式运行，调试时使用有界面模式

## 技术栈

- .NET 10.0
- Model Context Protocol SDK
- Playwright (浏览器自动化)
- Newtonsoft.Json

## License

MIT
