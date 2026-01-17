# 项目结构文档

## 概述
小黑盒 MCP 服务器项目采用分层架构设计，将代码分为三个项目：
- **XiaoheiheMcpServer.Shared**: 核心业务逻辑（服务和模型），被其他项目共享
- **XiaoheiheMcpServer.Stdio**: Stdio 传输协议实现（MCP 标准方式）
- **XiaoheiheMcpServer.Http**: HTTP 传输协议实现（可选替代方案）

所有项目都遵循"单一职责原则"，将原来的大型服务文件拆分为多个专业化的服务类。

## 项目关系图

```
Clients (LLM Apps, etc.)
    ↓
├─→ XiaoheiheMcpServer.Stdio  ─┐  (Stdio transport)
│   (Process-based)             │
│                               ├→ XiaoheiheMcpServer.Shared (Core Services)
└─→ XiaoheiheMcpServer.Http   ─┘  (HTTP transport)
    (Server-based)
```

```
src/
├── XiaoheiheMcpServer.Stdio/           # Stdio 传输协议实现（MCP Server 入口）
│   ├── Services/
│   │   └── XiaoheiheService.cs         # Facade 协调者（协调各专业服务）
│   ├── Program.cs                      # 应用入口和MCP工具定义
│   └── XiaoheiheMcpServer.Stdio.csproj # 项目文件
│
├── XiaoheiheMcpServer.Http/            # HTTP 传输协议实现（可选）
│   ├── XiaoheiheService.cs             # HTTP 版本的 Facade
│   ├── Program.cs                      # HTTP 服务器入口
│   ├── Properties/
│   │   └── launchSettings.json         # 启动配置
│   ├── appsettings.json                # 应用配置
│   ├── appsettings.Development.json    # 开发环境配置
│   └── XiaoheiheMcpServer.Http.csproj  # 项目文件
│
└── XiaoheiheMcpServer.Shared/          # 共享库（服务和模型）
    ├── Models/
    │   ├── McpToolResult.cs            # MCP 工具结果模型
    │   └── XiaoheiheModels.cs          # 业务模型（LoginStatus, PublishContentArgs 等）
    │
    └── Services/
        ├── BrowserBase.cs              # 浏览器基础类（处理Playwright生命周期、Cookies）
        ├── LoginService.cs             # 登录管理服务（二维码登录、状态检查）
        ├── PublishService.cs           # 内容发布服务（图文、文章、视频发布）
        ├── ArticlePublishService.cs    # 文章发布服务（长文章形式）
        ├── InteractionService.cs       # 互动服务（搜索、评论、获取详情）
        ├── CommonService.cs            # 通用服务方法
        └── XiaoheiheMcpServer.Shared.csproj # 项目文件
```

## 服务架构

所有服务都位于 `XiaoheiheMcpServer.Shared` 项目中，供 `Stdio` 和 `Http` 项目使用。

### 1. BrowserBase（浏览器基础类）
**职责**: Playwright 生命周期管理、Cookies 持久化
- `InitializeBrowserAsync()`: 初始化浏览器实例
- `SaveCookiesAsync()`: 保存登录 Cookies
- `LoadCookiesAsync()`: 加载保存的 Cookies
- `WaitForLoginAsync()`: 等待登录完成
- `DownloadImageAsBase64()`: 下载图片

**关键字段**:
- `_playwright`, `_browser`, `_context`, `_page`: Playwright 对象
- `_cookiesPath`: Cookies 文件路径
- `_headless`: 是否使用无头模式
- `_logger`: 日志记录器

### 2. LoginService（登录管理）
**职责**: 登录相关操作
- `CheckLoginStatusAsync()`: 检查当前登录状态
- `GetLoginQrCodeAsync()`: 获取扫码登录二维码

**使用场景**:
```csharp
var loginService = new LoginService(logger, headless: true);
var status = await loginService.CheckLoginStatusAsync();
if (!status.IsLoggedIn) {
    var qrInfo = await loginService.GetLoginQrCodeAsync();
}
```

### 3. PublishService（内容发布）
**职责**: 发布各类型内容
- `PublishContentAsync()`: 发布图文内容（图片+文字）
- `PublishArticleAsync()`: 发布文章（长文章形式）
- `PublishVideoAsync()`: 发布视频
- `UploadImagesAsync()`: 私有方法，处理图片上传的通用逻辑

**使用场景**:
```csharp
var publishService = new PublishService(logger, headless: true);

// 发布图文
var contentArgs = new PublishContentArgs {
    Title = "我的内容",
    Content = "内容正文",
    Images = new List<string> { "/path/to/image.jpg" },
    Tags = new List<string> { "标签1", "标签2" }
};
var result = await publishService.PublishContentAsync(contentArgs);

// 发布视频
var videoArgs = new PublishVideoArgs {
    Title = "我的视频",
    Description = "视频描述",
    VideoPath = "/path/to/video.mp4",
    CoverImagePath = "/path/to/cover.jpg"
};
var result = await publishService.PublishVideoAsync(videoArgs);
```

### 4. InteractionService（互动操作）
**职责**: 用户互动和内容查询
- `SearchAsync()`: 搜索内容
- `PostCommentAsync()`: 发表评论
- `GetPostDetailAsync()`: 获取帖子详情

**使用场景**:
```csharp
var interactionService = new InteractionService(logger, headless: true);

// 搜索
var searchResult = await interactionService.SearchAsync(new SearchArgs {
    Keyword = "游戏评测",
    PageSize = 20
});

// 评论
var commentResult = await interactionService.PostCommentAsync(new CommentArgs {
    PostId = "123456",
    Content = "很好的内容！"
});
```

### 5. XiaoheiheService（Facade 协调者）
**职责**: 协调各个专业服务，提供统一接口
- 依赖注入：`LoginService`, `PublishService`, `InteractionService`
- 所有公共方法都委托给相应的专业服务
- 添加统一的日志记录

**特点**:
- 使用 **Facade 设计模式**，简化客户端调用
- 内部管理各服务的生命周期
- 统一的错误处理和日志

**使用场景**:
```csharp
var service = new XiaoheiheService(logger, headless: true);

// 统一接口，自动委托给相应服务
var status = await service.CheckLoginStatusAsync();
var result = await service.PublishContentAsync(args);
var searchResult = await service.SearchAsync(args);

// 资源清理
await service.DisposeAsync();
```

## 依赖关系图

```
Program.cs (MCP Server)
    ↓
XiaoheiheService (Facade)
    ├── LoginService (extends BrowserBase)
    ├── PublishService (extends BrowserBase)
    ├── ArticlePublishService (extends BrowserBase)
    └── InteractionService (extends BrowserBase)
            ↓
        BrowserBase
            ↓
        Playwright
```

## 项目说明

### XiaoheiheMcpServer.Shared（共享库）
**职责**: 包含所有核心业务逻辑和数据模型
- **Models**: `McpToolResult`、业务模型（`LoginStatus`、`PublishContentArgs` 等）
- **Services**: 所有服务类（`LoginService`、`PublishService`、`InteractionService` 等）
- **特点**: 独立的业务库，不依赖 MCP 传输协议

### XiaoheiheMcpServer.Stdio（Stdio 实现）
**职责**: 通过 Stdio 传输协议提供 MCP 服务（推荐用于集成）
- **传输方式**: 标准输入/输出（Process-based）
- **适用场景**: LLM 应用集成、Claude 集成
- **优点**: 进程隔离、标准协议、启动快速

### XiaoheiheMcpServer.Http（HTTP 实现）
**职责**: 通过 HTTP 协议提供 MCP 服务（可选）
- **传输方式**: HTTP 长连接（Server-based）
- **适用场景**: Web 应用、远程调用、测试工具
- **优点**: 易于测试、跨机器通信、Web 兼容

## 依赖关系图

## 设计模式

### 1. 单一职责原则（SRP）
- 每个服务专注于一个职能领域
- `LoginService`: 仅处理登录
- `PublishService`: 仅处理发布
- `InteractionService`: 仅处理互动
- `BrowserBase`: 仅处理浏览器基础设施

### 2. Facade 模式
- `XiaoheiheService` 作为统一入口
- 简化复杂的服务协调
- 隐藏内部实现细节

### 3. 模板方法模式
- `BrowserBase` 定义了通用的浏览器操作模板
- 具体服务类继承并实现特定功能

### 4. 依赖注入
- 所有服务接受 `ILogger` 进行日志记录
- 支持构造函数注入式的灵活配置

## 迁移指南（如果需要更新代码）

### 旧代码（单一服务）
```csharp
var service = new XiaoheiheService(logger);
await service.PublishContentAsync(args);
```

### 新代码（拆分后）- 使用方式完全相同！
```csharp
var service = new XiaoheiheService(logger);
await service.PublishContentAsync(args);
// 内部已经委托给 PublishService
```

**优点**: API 完全向后兼容，客户端无需修改！

## 改进带来的好处

1. **可维护性提高**: 每个文件职责单一，易于理解和修改
2. **可测试性提高**: 每个服务可独立测试
3. **代码复用**: `BrowserBase` 中的通用代码被所有服务共享
4. **灵活扩展**: 新增功能只需添加新的服务类
5. **并发安全**: 每个服务可独立管理自己的浏览器实例
6. **错误隔离**: 一个服务的问题不会影响其他服务

## 文件大小对比

| 文件 | 行数 | 职责 |
|------|------|------|
| XiaoheiheService.cs (旧) | 918 | 所有操作 |
| BrowserBase.cs (新) | 137 | 浏览器基础 |
| LoginService.cs (新) | 105 | 登录管理 |
| PublishService.cs (新) | 382 | 内容发布 |
| InteractionService.cs (新) | 166 | 互动操作 |
| XiaoheiheService.cs (新) | 118 | Facade 协调 |

**总计**: 相同功能，代码分离，更清晰的结构！

## 后续改进方向

1. **接口提取**: 为各服务定义 `ILoginService`、`IPublishService` 等接口
2. **工厂模式**: 使用工厂来创建服务实例
3. **中间件**: 添加日志、异常处理中间件
4. **缓存**: 添加 Cookies、搜索结果缓存
5. **配置**: 提取硬编码的选择器到配置文件
6. **异步并发**: 支持多个发布操作并发执行

## 快速开始

### 编译整个解决方案
```bash
dotnet build
```

### 运行 Stdio 版本（推荐）
```bash
cd src/XiaoheiheMcpServer.Stdio
dotnet run -- --no-headless
```

### 运行 HTTP 版本（可选）
```bash
cd src/XiaoheiheMcpServer.Http
dotnet run -- --no-headless
```


### 发布单文件可执行程序
```bash
# 发布 Stdio 版本
cd src/XiaoheiheMcpServer.Stdio
dotnet publish -c Release -o ./publish

# 发布 HTTP 版本
cd src/XiaoheiheMcpServer.Http
dotnet publish -c Release -o ./publish
```

### 集成到你的 MCP 应用
```csharp
// Stdio 方式
using var process = new System.Diagnostics.Process
{
    StartInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "XiaoheiheMcpServer.Stdio.exe",
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }
};
process.Start();

// 或 HTTP 方式
var client = new HttpClient();
var response = await client.GetAsync("http://localhost:5000/mcp");
```

---
**最后更新**: 2026年1月17日  
**版本**: 0.4
