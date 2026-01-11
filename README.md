# 小黑盒 MCP 服务器

使用 .NET 实现的小黑盒社交网站 MCP (Model Context Protocol) 服务器，类似于 xiaohongshu-mcp 项目。

**基于最新的 MCP C# SDK (ModelContextProtocol)** - 使用依赖注入和特性（Attribute）方式注册工具。

## ⚠️ 开发状态

**本项目目前处于开发中**，部分功能尚未完全验证：
- ✅ 检查登录状态 - 已实现并测试
- ⚠️ 二维码登录 - 已实现但未验证
- ⚠️ 交互式登录 - 已实现但未验证
- ✅ 发布图文内容 - 已实现（包括图片、社区、话题）
- ⚠️ 发布文章 - 已实现但未验证
- ⚠️ 发布视频 - 已实现但未验证
- ⚠️ 发布评论 - 未验证
- ✅ 搜索内容 - 已实现并测试
- ✅ 获取帖子详情 - 已实现并测试

## 功能特性

- ✅ 检查登录状态
- ⚠️ 二维码登录（待验证）
- ⚠️ 交互式登录（待验证）
- ✅ 发布图文内容（支持图片、社区、话题）
- ⚠️ 发布文章（待验证）
- ⚠️ 发布视频（待验证）
- ⚠️ 发布评论
- ✅ 搜索内容
- ✅ 获取帖子详情

## 技术栈

- .NET 10.0
- ModelContextProtocol SDK (最新版)
- Microsoft.Extensions.Hosting (依赖注入)
- Playwright (浏览器自动化)
- Newtonsoft.Json

## 安装步骤

### 1. 克隆仓库

```bash
git clone https://github.com/wsmxd/XiaoheiheMcpServer.git
cd XiaoheiheMcpServer
```

### 2. 安装依赖

```bash
cd XiaoheiheMcpServer
dotnet restore
```

### 3. 安装 Playwright 浏览器

```bash
# 编译项目
dotnet build

# 安装 Chromium 浏览器
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## 使用方法

### 方式一：命令行运行

**无头模式（默认）**:
```bash
cd XiaoheiheMcpServer
dotnet run
```

**有界面模式（调试用）**:
```bash
cd XiaoheiheMcpServer
dotnet run -- --no-headless
```

### 方式二：在 Claude Desktop 中配置

编辑 Claude Desktop 配置文件：

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

添加以下配置：

```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<你的项目路径>/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"
      ],
      "env": {}
    }
  }
}
```

**注意**：请将 `<你的项目路径>` 替换为你的实际项目路径。

示例：
- Windows: `"D:/Projects/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"`
- macOS/Linux: `"/home/user/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"`

重启 Claude Desktop 后，服务器将自动启动。

### 方式三：在其他 MCP 客户端中使用

任何支持 stdio 传输的 MCP 客户端都可以使用本服务器。启动命令为：

```bash
dotnet run --project <项目路径>/XiaoheiheMcpServer.csproj
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
        "<你的项目路径>/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"
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
        "<你的项目路径>/XiaoheiheMcpServer/XiaoheiheMcpServer.csproj"
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
- 状态：✅ 已验证

### 2. interactive_login
交互式登录（打开浏览器手动登录）
- 参数：无
- 返回：登录结果
- 状态：⚠️ 待验证

### 3. get_login_qrcode
获取登录二维码
- 参数：无
- 返回：二维码图片（Base64）和过期时间
- 状态：⚠️ 待验证

### 4. publish_content
发布图文内容到小黑盒
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `images`: 图片路径列表（可选，本地绝对路径）
  - `communities`: 社区名称列表（可选，必须是已有的社区）
  - `tags`: 话题标签列表（可选）
- 状态：✅ 已实现（图片、社区、话题功能已添加）

### 5. publish_article
发布文章到小黑盒（长文章形式）
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `images`: 图片路径列表（可选）
  - `tags`: 标签列表（可选）
- 状态：⚠️ 待验证

### 6. publish_video
发布视频到小黑盒
- 参数：
  - `videoPath`: 视频文件路径（必需）
  - `title`: 标题（必需）
  - `description`: 描述（必需）
  - `coverImagePath`: 封面图片路径（可选）
  - `tags`: 标签列表（可选）
- 状态：⚠️ 待验证

### 7. search_content
搜索小黑盒内容
社区：原神方舟
话题：原神,游戏评测
```

```
搜索小黑盒上关于"原神"的内容
```

```
获取帖子 123456 的详细信息

### 8. get_post_detail
获取帖子详情
- 参数：
  - `postId`: 帖子ID（必需）
- 返回：封面图、标题、正文、标签、评论等详细信息
- 状态：✅ 已验证

### 9. post_comment
发表评论
- 参数：
  - `postId`: 帖子ID（必需）
  - `content`: 评论内容（必需）
- 状态：⚠️ 未验证

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

## 开发与测试

### 运行单元测试

本项目包含完整的单元测试套件：

```bash
dotnet test
```

测试覆盖：
- 6 个服务层测试（XiaoheiheServiceTests）
- 9 个模型验证测试（ModelsTests）

### 项目结构

```
XiaoheiheMcpServer/
├── Program.cs              # MCP 服务器入口（使用 Host Builder）
├── Models/
│   ├── XiaoheiheModels.cs  # 数据模型
│   └── McpToolResult.cs    # MCP 工具结果封装
├── Services/
│   └── XiaoheiheService.cs # 核心业务逻辑
└── data/
    └── cookies.json        # Cookie 存储

XiaoheiheMcpServer.Tests/
├── Services/
│   └── XiaoheiheServiceTests.cs  # 服务测试
└── Models/
    └── ModelsTests.cs            # 模型测试
```

## 注意事项

1. **浏览器安装**: 首次运行会下载 Chromium 浏览器（约 150MB）
2. **图片路径**: 图片路径需使用本地绝对路径
3. **页面选择器**: 请根据小黑盒实际页面结构调整选择器（在 XiaoheiheService.cs 中）
4. **运行模式**: 建议生产环境使用无头模式，调试时使用有界面模式
5. **登录有效期**: Cookie 过期后需要重新扫码登录

## 故障排查

### 问题：服务器无法启动
- 检查 .NET 10.0 SDK 是否已安装
- 确认 Playwright 浏览器已安装：`pwsh bin/Debug/net10.0/playwright.ps1 install chromium`

### 问题：登录失败或 Cookie 过期
- 删除 `data/cookies.json` 文件
- 重新运行服务器并调用 `get_login_qrcode` 工具

### 问题：找不到元素
- 小黑盒网站可能更新了页面结构
- 需要更新 `XiaoheiheService.cs` 中的 CSS 选择器

## 技术栈

- .NET 10.0
- ModelContextProtocol SDK (最新版)
- Microsoft.Extensions.Hosting (依赖注入)
- Playwright (浏览器自动化)
- Newtonsoft.Json
- xUnit + Moq (测试)

## License

MIT
