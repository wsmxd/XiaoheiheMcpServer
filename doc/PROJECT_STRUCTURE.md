# 项目结构文档

## 概述
小黑盒 MCP 服务器项目已根据"单一职责原则"进行重构，将原来的单一大型服务文件拆分为多个专业化的服务类。

## 项目结构

```
XiaoheiheMcpServer/
├── Models/
│   ├── McpToolResult.cs          # MCP 工具结果模型
│   └── XiaoheiheModels.cs        # 业务模型（LoginStatus, PublishContentArgs 等）
│
├── Services/
│   ├── BrowserBase.cs            # 浏览器基础类（处理Playwright生命周期、Cookies）
│   ├── LoginService.cs           # 登录管理服务（二维码登录、状态检查）
│   ├── PublishService.cs         # 内容发布服务（图文、文章、视频发布）
│   ├── InteractionService.cs     # 互动服务（搜索、评论、获取详情）
│   └── XiaoheiheService.cs       # Facade 协调者（协调各专业服务）
│
├── Program.cs                    # 应用入口和MCP工具定义
└── XiaoheiheMcpServer.csproj    # 项目文件

XiaoheiheMcpServer.Tests/
├── Services/
│   ├── BrowserBaseTests.cs       # BrowserBase 单元测试
│   ├── LoginServiceTests.cs      # LoginService 单元测试
│   ├── PublishServiceTests.cs    # PublishService 单元测试
│   ├── InteractionServiceTests.cs # InteractionService 单元测试
│   ├── XiaoheiheServiceTests.cs  # XiaoheiheService Facade 测试
│   └── ServiceIntegrationTests.cs # 服务集成测试
├── Models/
│   └── ModelsTests.cs            # 模型测试
└── XiaoheiheMcpServer.Tests.csproj
```

## 服务架构

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
    └── InteractionService (extends BrowserBase)
            ↓
        BrowserBase
            ↓
        Playwright
```

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

## 测试结构

### 单元测试
```
BrowserBaseTests        - 基础类功能测试
LoginServiceTests       - 登录服务测试
PublishServiceTests     - 发布服务测试
InteractionServiceTests - 互动服务测试
XiaoheiheServiceTests   - Facade 测试
```

### 集成测试
```
ServiceIntegrationTests - 多服务协同工作测试
```

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

### 编译项目（无头模式）
```bash
cd XiaoheiheMcpServer
dotnet build
```

### 运行服务器（有界面模式）
```bash
dotnet run -- --no-headless
```

### 运行测试
```bash
dotnet test
```

### 集成到你的 MCP 应用
```csharp
var service = new XiaoheiheService(logger, headless: true);

// 所有原来的 API 都可用
await service.CheckLoginStatusAsync();
await service.PublishContentAsync(args);
// ... 其他操作
```

---
**最后更新**: 2026年1月11日  
**版本**: 2.0（架构重构版）
