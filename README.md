# 小黑盒 MCP 服务器

使用 .NET 实现的小黑盒社交网站 MCP (Model Context Protocol) 服务器，类似于 xiaohongshu-mcp 项目。

## 技术栈

- .NET 10.0
- ModelContextProtocol SDK
- Microsoft.Extensions.Hosting (依赖注入)
- Playwright (浏览器自动化)
- Newtonsoft.Json

## 安装步骤

### 方式一：使用发布的 HTTP 包（推荐）

1. 下载发布压缩包（内含 `XiaoheiheMcpServer.Http.exe` 与脚本）
2. 解压到任意目录，例如：`D:\Tools\XiaoheiheMcpServer\`
3. 安装浏览器依赖：`install-chromium.bat`（仅首次需要）
4. 直接运行：`XiaoheiheMcpServer.Http.exe`（默认监听 HTTP 端口 5000）

### 方式二：使用发布的单文件 exe（stdio）

1. 从 Releases 下载 `XiaoheiheMcpServer-win-x64.zip`
2. 解压到任意目录，例如：`D:\Tools\XiaoheiheMcpServer\`


### 方式三：从源码构建

1. 克隆仓库并还原依赖
  ```bash
  git clone https://github.com/wsmxd/XiaoheiheMcpServer.git
  cd XiaoheiheMcpServer
  dotnet restore
  ```
2. 构建并安装 Playwright 浏览器
  ```bash
  dotnet build
  .\playwright.ps1 install
  或者使用初始化install-chromium.bat脚本来安装
  ```
3. 运行（开发模式）
  ```bash
  dotnet run --project src/XiaoheiheMcpServer.Stdio/XiaoheiheMcpServer.Stdio.csproj
  ```

## MCP 客户端配置

### VSCode 配置

**使用发布的 HTTP 包（推荐）**：
```json
{
  "servers": {
    "xiaoheihe": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  },
  "inputs": []
}
```

**使用发布的 stdio exe**：
```json
{
  "servers": {
    "xiaoheihe": {
      "type": "stdio",
      "command": "D:\\Tools\\XiaoheiheMcpServer\\XiaoheiheMcpServer.Stdio.exe",
      "args": []
    }
  },
  "inputs": []
}
```

**注意**：
- 将路径替换为你的实际安装/项目路径
- Windows 路径使用双反斜杠 `\\` 或单斜杠 `/`
- 配置后需重启客户端

## 首次使用

1. **先跑一次初始化脚本**（发布包内已附带）
  - CMD: `install-chromium.bat`
  - 作用：安装 Playwright 浏览器

2. **使用参数来决定模式**：
  - 默认是**无头模式**（后台运行）
  - 完成登录后，Cookie 自动保存到 `data/cookies.json`
  - 使用`--show-browser`参数启动**有头模式**（显示浏览器界面）

3. **推荐登录方式**：
  - 使用 `get_login_qr_code` 工具（获返回二维码并调用系统工具打开图片）

4. **测试连接**：
   ```
   请帮我检查小黑盒登录状态
   ```

### 1. check_login_status
检查小黑盒登录状态
- 参数：无
- 返回：登录状态和用户名
- 状态：✅ 已验证

### 2. interactive_login
交互式登录（需要在有头模式才行）
- 参数：无
- 返回：登录结果
- 状态：✅ 手动进行登录就行

### 3. publish_content
发布图文内容到小黑盒
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `images`: 图片路径列表（可选，本地绝对路径）
  - `communities`: 社区名称列表（可选，必须是已有的社区，**最多2个**）
  - `tags`: 话题标签列表（可选，**最多5个**）
- 状态：✅ 已实现（图片、社区、话题功能已添加）

### 4. publish_article
发布文章到小黑盒（长文章形式）
- 参数：
  - `title`: 标题（必需）
  - `content`: 内容（必需，可包含本地图片绝对路径，将自动识别并上传）
  - `communities`: 社区名称列表（必需，必须是已有的社区，**最多2个**）
  - `tags`: 标签列表（可选，**最多5个**）
- 状态：✅ 已验证

### 5. publish_video
发布视频到小黑盒
- 参数：
  - `videoPath`: 视频文件路径（必需）
  - `title`: 标题（必需）
  - `content`: 内容（必需）
  - `coverImagePath`: 封面图片路径（可选，建议提供）
  - `communities`: 社区名称列表（可选，必须是已有的社区，**最多2个**）
  - `tags`: 标签列表（可选，**最多5个**）
- 状态：✅ 已验证

### 6. search_content
搜索小黑盒内容

```
搜索小黑盒上关于"原神"的内容
```

获取帖子 123456 的详细信息

### 7. get_post_detail
获取帖子/文章详情
- 参数：
  - `postId`: 帖子/文章ID（必需）
- 返回：封面图、标题、正文、标签、评论等详细信息
- 支持：图文帖子和长文章两种类型
- 状态：✅ 已验证

### 8. post_comment
发表评论
- 参数：
  - `postId`: 帖子ID（必需）
  - `content`: 评论内容（必需）
  - `images`: 评论图片路径列表（可选，本地绝对路径）
- 状态：✅ 已验证

## 数据存储

- Cookies 存储在 `data/cookies.json`
- 登录后会自动保存 Cookies，下次启动无需重新登录


## 注意事项

1. **浏览器安装**: 首次运行必须下载 Chromium 浏览器（约 150MB）
2. **图片路径**: 图片路径需使用本地绝对路径
3. **页面选择器**: 请根据小黑盒实际页面结构调整选择器（在 XiaoheiheService.cs 中）
4. **运行模式**: 建议生产环境使用无头模式，调试时使用有界面模式
5. **登录有效期**: Cookie 过期后需要重新扫码登录

## 故障排查

### 问题：服务器无法启动
- 检查是否未安装playwright浏览器，重新运行 `install-chromium.bat` 或执行 `playwright install chromium` 安装浏览器

### 问题：登录失败或 Cookie 过期
- 删除 `data/cookies.json` 文件
- 重新运行服务器并调用 `get_login_qrcode` 工具或者使用命令行参数`--show-browser`来使用有头模式进行重新登录

### 问题：找不到元素
- 小黑盒网站可能更新了页面结构
- 需要更新 `XiaoheiheService.cs` 中的 CSS 选择器

## License

[MIT](LICENSE)
