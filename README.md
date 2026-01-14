# 小黑盒 MCP 服务器

使用 .NET 实现的小黑盒社交网站 MCP (Model Context Protocol) 服务器，类似于 xiaohongshu-mcp 项目。

**基于最新的 MCP C# SDK (ModelContextProtocol)** - 使用依赖注入和特性（Attribute）方式注册工具。

## ⚠️ 开发状态

**本项目目前处于开发中**，部分功能尚未完全验证：
- ✅ 检查登录状态 - 已实现并测试
- ✅ 二维码登录 - 已实现但未验证
- ✅ 交互式登录 - 已实现
- ✅ 发布图文内容 - 已实现（包括图片、社区、话题）
- ✅ 发布文章 - 已实现
- ⚠️ 发布视频 - 已实现但未验证
- ✅ 发布评论 - 已验证
- ✅ 搜索内容 - 已实现并测试
- ✅ 获取帖子详情 - 已实现并测试

## 功能特性

- ✅ 检查登录状态
- ✅ 二维码登录
- ✅ 交互式登录
- ✅ 发布图文内容（支持图片、社区、话题）
- ✅ 发布文章
- ⚠️ 发布视频（待验证）
- ✅ 发布评论
- ✅ 搜索内容
- ✅ 获取帖子详情

## 技术栈

- .NET 10.0
- ModelContextProtocol SDK (最新版)
- Microsoft.Extensions.Hosting (依赖注入)
- Playwright (浏览器自动化)
- Newtonsoft.Json

## 安装步骤

### 方式一：使用发布的单文件 exe（推荐）

1. 从 Releases 下载最新的 `XiaoheiheMcpServer-win-x64.zip`
2. 解压到任意目录，例如：`D:\Tools\XiaoheiheMcpServer\`
3. 运行初始化脚本（会检查 .NET 运行时并安装 Playwright 浏览器）：
  - PowerShell: `./setup.ps1`
  - CMD: `setup.bat`
4. 运行服务器：`./XiaoheiheMcpServer.exe`（首次登录可加 `--no-headless`）

> 说明：发布包内只包含单个 exe + 两个初始化脚本，依赖下载在首次安装时完成。

### 方式二：从源码构建

1. 克隆仓库并还原依赖
  ```bash
  git clone https://github.com/wsmxd/XiaoheiheMcpServer.git
  cd XiaoheiheMcpServer
  dotnet restore
  ```
2. 构建并安装 Playwright 浏览器
  ```bash
  dotnet build
  playwright install chromium
  ```
3. 运行（开发模式）
  ```bash
  dotnet run --project XiaoheiheMcpServer/XiaoheiheMcpServer.csproj
  ```

## MCP 客户端配置

### VSCode 配置

在任意目录下建立.vscode文件夹，创建mcp.json文件：

**使用发布的 exe（推荐）**：
```json
{
	"servers": {
		"xiaoheihe": {
			"type": "stdio",
			"command": "D:\\Tools\\XiaoheiheMcpServer\\XiaoheiheMcpServer.exe",
			"args": []
		}
	},
	"inputs": []
}
```

**从源码运行（开发模式）**：
```json
{
  "servers": {
    "xiaoheihe": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\Projects\\XiaoheiheMcpServer\\XiaoheiheMcpServer\\XiaoheiheMcpServer.csproj"
      ]
    }
  },
  "inputs": []
}
```

### Cursor 配置

编辑配置文件（`.cursor/mcp.json`）：

**使用发布的 exe（推荐）**：
```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "D:\\Tools\\XiaoheiheMcpServer\\XiaoheiheMcpServer.exe",
      "args": []
    }
  }
}
```

**从源码运行（开发模式）**：
```json
{
  "mcpServers": {
    "xiaoheihe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\Projects\\XiaoheiheMcpServer\\XiaoheiheMcpServer\\XiaoheiheMcpServer.csproj"
      ]
    }
  }
}
```

**注意**：
- 将路径替换为你的实际安装/项目路径
- Windows 路径使用双反斜杠 `\\` 或单斜杠 `/`
- 配置后需重启客户端

## 首次使用

1. **先跑一次初始化脚本**（发布包内已附带）
  - PowerShell: `./setup.ps1`
  - CMD: `setup.bat`
  - 作用：检测/提示安装 .NET 运行时，安装 Playwright 浏览器

2. **自动模式切换**：
  - 首次运行会自动使用**有头模式**（显示浏览器窗口）
  - 完成登录后，Cookie 自动保存到 `data/cookies.json`
  - 后续运行自动切换为**无头模式**（后台运行）

3. **推荐登录方式**：
  - 使用 `interactive_login` 工具（在浏览器中手动登录）
  - 支持手机验证码、密码、扫码等多种方式

4. **测试连接**：
   ```
   请帮我检查小黑盒登录状态
   ```

## 命令行使用（可选）

如果需要单独测试，可以直接运行（首次请先执行 `setup.ps1` 或 `setup.bat` 安装依赖）：

**发布版**：
```powershell
cd D:\Tools\XiaoheiheMcpServer
.\XiaoheiheMcpServer.exe
```

**开发版**：
```bash
cd XiaoheiheMcpServer
dotnet run
```

### 1. check_login_status
检查小黑盒登录状态
- 参数：无
- 返回：登录状态和用户名
- 状态：✅ 已验证

### 2. interactive_login
交互式登录（打开浏览器手动登录）
- 参数：无
- 返回：登录结果
- 状态：✅ 手动进行登录就行

### 3. get_login_qrcode
获取登录二维码
- 参数：无
- 返回：二维码图片（Base64）和过期时间
- 状态：✅ 已验证

### 4. publish_content
发布图文内容到小黑盒
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `images`: 图片路径列表（可选，本地绝对路径）
  - `communities`: 社区名称列表（可选，必须是已有的社区，**最多2个**）
  - `tags`: 话题标签列表（可选，**最多5个**）
- 状态：✅ 已实现（图片、社区、话题功能已添加）

### 5. publish_article
发布文章到小黑盒（长文章形式）
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需，可包含本地图片绝对路径，将自动识别并上传）
  - `communities`: 社区名称列表（必需，必须是已有的社区，**最多2个**）
  - `tags`: 标签列表（可选，**最多5个**）
- 状态：✅ 已验证

### 6. publish_video
发布视频到小黑盒
- 参数：
  - `videoPath`: 视频文件路径（必需）
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `coverImagePath`: 封面图片路径（可选，建议提供）
  - `communities`: 社区名称列表（可选，必须是已有的社区，**最多2个**）
  - `tags`: 标签列表（可选，**最多5个**）
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
获取帖子/文章详情
- 参数：
  - `postId`: 帖子/文章ID（必需）
- 返回：封面图、标题、正文、标签、评论等详细信息
- 支持：图文帖子和长文章两种类型
- 状态：✅ 已验证

### 9. post_comment
发表评论
- 参数：
  - `postId`: 帖子ID（必需）
  - `content`: 评论内容（必需）
  - `images`: 评论图片路径列表（可选，本地绝对路径）
- 状态：✅ 已验证

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

1. **浏览器安装**: 首次运行会下载 Chromium 浏览器（约 150MB）
2. **图片路径**: 图片路径需使用本地绝对路径
3. **页面选择器**: 请根据小黑盒实际页面结构调整选择器（在 XiaoheiheService.cs 中）
4. **运行模式**: 建议生产环境使用无头模式，调试时使用有界面模式
5. **登录有效期**: Cookie 过期后需要重新扫码登录

## 故障排查

### 问题：服务器无法启动
- 检查 .NET 10.0 运行时是否已安装（缺失可通过 `setup.ps1`/`setup.bat` 提示的链接安装）
- 重新运行 `setup.ps1` 或执行 `playwright install chromium` 安装浏览器

### 问题：登录失败或 Cookie 过期
- 删除 `data/cookies.json` 文件
- 重新运行服务器并调用 `get_login_qrcode` 工具或者使用命令行参数`--no-headless`来使用有头模式进行重新登录

### 问题：找不到元素
- 小黑盒网站可能更新了页面结构
- 需要更新 `XiaoheiheService.cs` 中的 CSS 选择器

## 技术栈

- .NET 10.0
- ModelContextProtocol SDK (最新版)
- Microsoft.Extensions.Hosting (依赖注入)
- Playwright (浏览器自动化)
- Newtonsoft.Json

## License

MIT
