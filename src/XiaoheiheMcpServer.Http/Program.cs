using System.Text.Json;
using System.Text.Json.Nodes;
using XiaoheiheMcpServer.Http;
using XiaoheiheMcpServer.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

var headless = !args.Contains("--show-browser");
headless = false; // 临时强制有头模式，方便调试
// 注册XiaoheiheService为单例，默认使用无头模式
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<XiaoheiheService>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new XiaoheiheService(logger, loggerFactory, headless);
});

// 配置CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// 根路径 - 服务器信息
app.MapGet("/", () => new
{
    name = "小黑盒MCP HTTP服务器",
    version = "1.0.0",
    protocol = "MCP over HTTP (JSON-RPC 2.0)",
    endpoint = "POST /mcp",
    tools = new[]
    {
        "check_login_status - 检查登录状态",
        "interactive_login - 交互式登录",
        "get_login_qr_code - 使用二维码进行登录",
        "get_user_profile - 获取用户个人信息",
        "publish_content - 发布图文内容",
        "publish_article - 发布文章",
        "publish_video - 发布视频",
        "get_home_content - 获取首页内容",
        "search_content - 搜索内容",
        "get_post_detail - 获取帖子详情",
        "post_comment - 发表评论",
        "reply_comment - 回复评论"
    }
});

// MCP 统一端点 - 符合 JSON-RPC 2.0 规范
app.MapPost("/mcp", async (HttpContext context, XiaoheiheService service, ILogger<Program> logger) =>
{
    try
    {
        // 解析 JSON-RPC 请求
        var requestBody = await JsonSerializer.DeserializeAsync<JsonNode>(context.Request.Body);
        
        if (requestBody == null)
        {
            return Results.Json(new
            {
                jsonrpc = "2.0",
                error = new { code = -32700, message = "Parse error" },
                id = (object?)null
            });
        }

        // JSON-RPC 2.0: id 可以是 string, number 或 null
        var id = requestBody["id"]?.AsValue().GetValue<object>();
        var method = requestBody["method"]?.GetValue<string>();
        var @params = requestBody["params"]?.AsObject();

        if (string.IsNullOrEmpty(method))
        {
            return Results.Json(new
            {
                jsonrpc = "2.0",
                error = new { code = -32600, message = "Invalid Request - missing method" },
                id
            });
        }

        logger.LogInformation("收到MCP请求: {Method}", method);

        // 根据 method 调用对应的 MCP 协议方法或工具
        object? result = method switch
        {
            // MCP 协议标准方法
            "initialize" => HandleInitialize(@params),
            "tools/list" => HandleToolsList(),
            "tools/call" => await HandleToolsCall(service, @params, logger),
            "logging/setLevel" => HandleLoggingSetLevel(@params),
            "ping" => new { },
            
            // 向后兼容：直接调用工具方法（非标准）
            "check_login_status" => await HandleCheckLoginStatus(service),
            "interactive_login" => await HandleInteractiveLogin(service, @params),
            "get_login_qr_code" => await HandleGetLoginQrCode(service, @params),
            "get_user_profile" => await HandleGetUserProfile(service, @params),
            "publish_content" => await HandlePublishContent(service, @params),
            "publish_article" => await HandlePublishArticle(service, @params),
            "publish_video" => await HandlePublishVideo(service, @params),
            "search_content" => await HandleSearchContent(service, @params),
            "get_home_content" => await HandleGetHomeContent(service),
            "get_post_detail" => await HandleGetPostDetail(service, @params),
            "post_comment" => await HandlePostComment(service, @params),
            "reply_comment" => await HandleReplyComment(service, @params),
            
            _ => new { error = new { code = -32601, message = $"Method not found: {method}" } }
        };

        // 检查是否有错误
        if (result is JsonObject jsonObj && jsonObj.ContainsKey("error"))
        {
            return Results.Json(new
            {
                jsonrpc = "2.0",
                error = jsonObj["error"],
                id
            });
        }

        // 返回成功响应
        return Results.Json(new
        {
            jsonrpc = "2.0",
            result,
            id
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "处理MCP请求时出错");
        return Results.Json(new
        {
            jsonrpc = "2.0",
            error = new { code = -32603, message = "Internal error", data = ex.Message },
            id = (string?)null
        });
    }
});

#region MCP 协议标准方法

object HandleInitialize(JsonObject? @params)
{
    return new
    {
        protocolVersion = "2024-11-05",
        serverInfo = new
        {
            name = "小黑盒MCP服务器",
            version = "1.0.0"
        },
        capabilities = new
        {
            tools = new { }
        }
    };
}

object HandleToolsList()
{
    return new
    {
        tools = new object[]
        {
            new
            {
                name = "check_login_status",
                description = "检查小黑盒登录状态",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "interactive_login",
                description = "打开浏览器窗口，让用户手动登录小黑盒（只有有头模式才行，启动程序的时候传入 --show-browser 参数）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        waitTimeoutSeconds = new { type = "number", description = "等待用户登录的超时时间（秒），默认180秒" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "get_login_qr_code",
                description = "获取登录二维码然后回自动打开图片让用户扫描，返回登录的结果（适合无头模式）",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "get_user_profile",
                description = "获取用户个人信息（需要登录状态）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageSize = new { type = "number", description = "每页条数，默认10" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "publish_content",
                description = "发布图文内容到小黑盒",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "内容标题" },
                        content = new { type = "string", description = "正文内容" },
                        images = new { type = "array", items = new { type = "string" }, description = "图片路径列表" },
                        communities = new { type = "array", items = new { type = "string" }, description = "社区名称列表（最多2个）" },
                        tags = new { type = "array", items = new { type = "string" }, description = "话题标签列表（最多5个）" }
                    },
                    required = new[] { "title", "content" }
                }
            },
            new
            {
                name = "publish_article",
                description = "发布文章到小黑盒（长文章形式）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "文章标题" },
                        content = new { type = "string", description = "文章正文" },
                        communities = new { type = "array", items = new { type = "string" }, description = "社区名称列表（最多2个）" },
                        tags = new { type = "array", items = new { type = "string" }, description = "话题标签列表（最多5个）" }
                    },
                    required = new[] { "title", "content", "communities" }
                }
            },
            new
            {
                name = "publish_video",
                description = "发布视频到小黑盒",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "视频标题" },
                        content = new { type = "string", description = "视频描述" },
                        videoPath = new { type = "string", description = "视频文件路径" },
                        coverImagePath = new { type = "string", description = "封面图路径" },
                        communities = new { type = "array", items = new { type = "string" }, description = "社区名称列表（最多2个）" },
                        tags = new { type = "array", items = new { type = "string" }, description = "话题标签列表（最多5个）" }
                    },
                    required = new[] { "title", "content", "videoPath", "coverImagePath" }
                }
            },
            new
            {
                name = "get_home_content",
                description = "获取小黑盒首页内容",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "search_content",
                description = "搜索小黑盒内容",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        keyword = new { type = "string", description = "搜索关键词" },
                        page = new { type = "number", description = "页码" },
                        pageSize = new { type = "number", description = "每页数量（最多20）" }
                    },
                    required = new[] { "keyword" }
                }
            },
            new
            {
                name = "get_post_detail",
                description = "获取小黑盒帖子详情",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        postId = new { type = "string", description = "帖子ID" }
                    },
                    required = new[] { "postId" }
                }
            },
            new
            {
                name = "post_comment",
                description = "发表评论到小黑盒帖子",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        postId = new { type = "string", description = "帖子ID" },
                        content = new { type = "string", description = "评论内容" },
                        images = new { type = "array", items = new { type = "string" }, description = "评论图片路径列表" }
                    },
                    required = new[] { "postId", "content" }
                }
            },
            new
            {
                name = "reply_comment",
                description = "回复评论（需要先到帖子的详情信息页然后获取到具体的评论元素，然后右键点击该评论元素获取到回复按钮元素，最后调用这个工具传入回复内容即可）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        targetCommentContent = new { type = "string", description = "要回复的目标评论的内容，用于定位到具体的评论元素" },
                        content = new { type = "string", description = "回复内容" }
                    },
                    required = new[] { "targetCommentContent", "content" }
                }
            }
        }
    };
}

async Task<object> HandleToolsCall(XiaoheiheService service, JsonObject? @params, ILogger<Program> logger)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var toolName = @params["name"]?.GetValue<string>();
    var toolParams = @params["arguments"]?.AsObject();

    if (string.IsNullOrEmpty(toolName))
        return new { error = new { code = -32602, message = "Missing tool name" } };

    logger.LogInformation("调用工具: {ToolName}", toolName);

    try
    {
        var result = toolName switch
        {
            "check_login_status" => await HandleCheckLoginStatus(service),
            "interactive_login" => await HandleInteractiveLogin(service, toolParams),
            "get_login_qr_code" => await HandleGetLoginQrCode(service, toolParams),
            "get_user_profile" => await HandleGetUserProfile(service, toolParams),
            "publish_content" => await HandlePublishContent(service, toolParams),
            "publish_article" => await HandlePublishArticle(service, toolParams),
            "publish_video" => await HandlePublishVideo(service, toolParams),
            "get_home_content" => await HandleGetHomeContent(service),
            "search_content" => await HandleSearchContent(service, toolParams),
            "get_post_detail" => await HandleGetPostDetail(service, toolParams),
            "post_comment" => await HandlePostComment(service, toolParams),
            "reply_comment" => await HandleReplyComment(service, toolParams),
            _ => new { error = new { code = -32601, message = $"Unknown tool: {toolName}" } }
        };

        // 如果工具返回错误，直接返回
        if (result is JsonObject jsonObj && jsonObj.ContainsKey("error"))
            return result;

        // 包装成 MCP 工具调用结果格式
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(result)
                }
            }
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "工具调用失败: {ToolName}", toolName);
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"错误: {ex.Message}"
                }
            },
            isError = true
        };
    }
}

object HandleLoggingSetLevel(JsonObject? @params)
{
    // 简单实现，接受但不做实际处理
    var level = @params?["level"]?.GetValue<string>();
    return new { };
}

#endregion

#region 参数提取辅助方法

T? DeserializeParams<T>(JsonObject? @params, ILogger? logger = null) where T : class
{
    if (@params == null)
        return null;

    try
    {
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        var result = JsonSerializer.Deserialize<T>(@params.ToJsonString(), options);
        return result;
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "反序列化参数失败");
        return null;
    }
}

#endregion

#region MCP 工具处理器

Task<LoginStatus> HandleCheckLoginStatus(XiaoheiheService service) => service.CheckLoginStatusAsync();

async Task<object> HandleInteractiveLogin(XiaoheiheService service, JsonObject? @params)
{
    var waitTimeoutSeconds = 180; // 默认180秒
    
    if (@params != null && @params.TryGetPropertyValue("waitTimeoutSeconds", out var timeoutNode))
    {
        waitTimeoutSeconds = timeoutNode?.GetValue<int>() ?? 180;
    }
    
    var status = await service.InteractiveLoginAsync(waitTimeoutSeconds);
    return status.IsLoggedIn
            ? $"✅ {status.Message}\n用户名: {status.Username}\n\n现在可以使用其他功能了！"
            : $"❌ {status.Message}";
}

async Task<object> HandleGetLoginQrCode(XiaoheiheService service, JsonObject? @params)
{
    var qrCodeInfo = await service.GetLoginQrCodeAsync();
    if (qrCodeInfo == null)
    {
        return new { error = new { code = -32603, message = "Failed to get QR code" } };
    }

    return qrCodeInfo;
}

async Task<object> HandleGetUserProfile(XiaoheiheService service, JsonObject? @params)
{
    var pageSize = 10; // 默认每页10条
    if (@params != null && @params.TryGetPropertyValue("pageSize", out var pageSizeNode))
    {
        pageSize = pageSizeNode?.GetValue<int>() ?? 10;
    }

    return await service.GetUserProfileAsync(pageSize);
}

async Task<object> HandlePublishContent(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<PublishContentArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.Title) || string.IsNullOrEmpty(args.Content))
        return new { error = new { code = -32602, message = "Missing or invalid required parameters: title, content" } };

    if (args.Communities?.Count > 2)
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags?.Count > 5)
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    return await service.PublishContentAsync(args);
}

async Task<object> HandlePublishArticle(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<PublishArticleArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.Title) || string.IsNullOrEmpty(args.Content) || args.Communities?.Count == 0)
        return new { error = new { code = -32602, message = "Missing or invalid required parameters: title, content, communities" } };

    if (args.Communities?.Count > 2)
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags?.Count > 5)
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    return await service.PublishArticleAsync(args);
}

async Task<object> HandlePublishVideo(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<PublishVideoArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.Title) || string.IsNullOrEmpty(args.Content) || 
        string.IsNullOrEmpty(args.VideoPath) || string.IsNullOrEmpty(args.CoverImagePath))
        return new { error = new { code = -32602, message = "Missing or invalid required parameters: title, content, videoPath, coverImagePath" } };

    if (args.Communities?.Count > 2)
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags?.Count > 5)
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    return await service.PublishVideoAsync(args);
}

async Task<object> HandleGetHomeContent(XiaoheiheService service)
{
    return await service.GetHomeContentAsync();
}

async Task<object> HandleSearchContent(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<SearchArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.Keyword))
        return new { error = new { code = -32602, message = "Missing or invalid required parameter: keyword" } };

    return await service.SearchContentAsync(args);
}

async Task<object> HandleGetPostDetail(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<PostDetailArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.PostId))
        return new { error = new { code = -32602, message = "Missing or invalid required parameter: postId" } };

    return await service.GetPostDetailAsync(args);
}

async Task<object> HandlePostComment(XiaoheiheService service, JsonObject? @params)
{
    var args = DeserializeParams<CommentArgs>(@params);
    if (args == null || string.IsNullOrEmpty(args.PostId) || string.IsNullOrEmpty(args.Content))
        return new { error = new { code = -32602, message = "Missing or invalid required parameters: postId, content" } };

    return await service.PostCommentAsync(args);
}

async Task<object> HandleReplyComment(XiaoheiheService service, JsonObject? @params)
{
    var targetCommentContent = @params?["targetCommentContent"]?.GetValue<string>();
    var content = @params?["content"]?.GetValue<string>();

    if (string.IsNullOrEmpty(targetCommentContent) || string.IsNullOrEmpty(content))
        return new { error = new { code = -32602, message = "Missing or invalid required parameters: targetCommentContent, content" } };

    return await service.ReplyCommentAsync(content, targetCommentContent);
}

#endregion

// 在应用停止时清理资源
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(async () =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("正在关闭HTTP服务器，清理资源...");
    var service = app.Services.GetService<XiaoheiheService>();
    if (service != null)
    {
        await service.DisposeAsync();
    }
});

app.Run();
