using System.Text.Json;
using System.Text.Json.Nodes;
using XiaoheiheMcpServer.Http;
using XiaoheiheMcpServer.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// 检查是否首次使用（是否存在 Cookie 文件）
var headless = true;
var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
var cookiePath = Path.Combine(dataDir, "cookies.json");
var isFirstTime = !File.Exists(cookiePath);
if (isFirstTime)
{
    Console.WriteLine("首次运行检测：未找到 cookies.json，可能需要先进行登录。");
    headless = false;
}

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
        "publish_content - 发布图文内容",
        "publish_article - 发布文章",
        "publish_video - 发布视频",
        "search_content - 搜索内容",
        "get_post_detail - 获取帖子详情",
        "post_comment - 发表评论"
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
            "publish_content" => await HandlePublishContent(service, @params),
            "publish_article" => await HandlePublishArticle(service, @params),
            "publish_video" => await HandlePublishVideo(service, @params),
            "search_content" => await HandleSearchContent(service, @params),
            "get_post_detail" => await HandleGetPostDetail(service, @params),
            "post_comment" => await HandlePostComment(service, @params),
            
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
                description = "打开浏览器窗口，让用户手动登录小黑盒（推荐首次登录使用）",
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
            "publish_content" => await HandlePublishContent(service, toolParams),
            "publish_article" => await HandlePublishArticle(service, toolParams),
            "publish_video" => await HandlePublishVideo(service, toolParams),
            "search_content" => await HandleSearchContent(service, toolParams),
            "get_post_detail" => await HandleGetPostDetail(service, toolParams),
            "post_comment" => await HandlePostComment(service, toolParams),
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

async Task<object> HandlePublishContent(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<PublishContentArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    if (args.Communities is { Count: > 2 })
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags is { Count: > 5 })
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    var result = await service.PublishContentAsync(args);
    return result;
}

async Task<object> HandlePublishArticle(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<PublishArticleArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    if (args.Communities.Count > 2)
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags is { Count: > 5 })
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    var result = await service.PublishArticleAsync(args);
    return result;
}

async Task<object> HandlePublishVideo(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<PublishVideoArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    if (args.Communities is { Count: > 2 })
        return new { error = new { code = -32602, message = "communities 最多只能传 2 个" } };
    if (args.Tags is { Count: > 5 })
        return new { error = new { code = -32602, message = "tags 最多只能传 5 个" } };

    var result = await service.PublishVideoAsync(args);
    return result;
}

async Task<object> HandleSearchContent(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<SearchArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    var result = await service.SearchContentAsync(args);
    return result;
}

async Task<object> HandleGetPostDetail(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<PostDetailArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    var result = await service.GetPostContentAsync(args);
    return result;
}

async Task<object> HandlePostComment(XiaoheiheService service, JsonObject? @params)
{
    if (@params == null)
        return new { error = new { code = -32602, message = "Invalid params" } };

    var args = JsonSerializer.Deserialize<CommentArgs>(@params.ToJsonString());
    if (args == null)
        return new { error = new { code = -32602, message = "Invalid params format" } };

    var result = await service.PostCommentAsync(args);
    return result;
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
