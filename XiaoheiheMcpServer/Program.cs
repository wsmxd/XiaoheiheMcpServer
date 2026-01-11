using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Models;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer;

/// <summary>
/// MCP服务器主程序 - 使用stdio传输
/// </summary>
public class Program
{
    private static XiaoheiheService? _xiaoheiheService;
    private static ILogger<Program>? _logger;

    public static async Task Main(string[] args)
    {
        // 设置日志
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = loggerFactory.CreateLogger<Program>();
        
        // 解析命令行参数
        var headless = !args.Contains("--no-headless");
        
        _logger.LogInformation("小黑盒MCP服务器启动中...");
        _logger.LogInformation($"无头模式: {headless}");

        // 初始化小黑盒服务
        var xiaoheiheLogger = loggerFactory.CreateLogger<XiaoheiheService>();
        _xiaoheiheService = new XiaoheiheService(xiaoheiheLogger, headless);

        // 创建MCP服务器
        var server = new McpServer(
            new ServerInfo
            {
                Name = "xiaoheihe-mcp",
                Version = "1.0.0"
            },
            new ServerCapabilities
            {
                Tools = new ToolsCapability()
            }
        );

        // 注册工具
        RegisterTools(server);

        _logger.LogInformation("MCP服务器已启动，等待连接...");

        // 使用stdio传输
        await server.ConnectAsync(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput()
        );

        _logger.LogInformation("MCP服务器正在运行...");

        // 等待直到进程被终止
        await Task.Delay(Timeout.Infinite);
    }

    private static void RegisterTools(McpServer server)
    {
        // 工具1: 检查登录状态
        server.AddTool(new Tool
        {
            Name = "check_login_status",
            Description = "检查小黑盒登录状态",
            InputSchema = new { }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: check_login_status");
            var status = await _xiaoheiheService!.CheckLoginStatusAsync();
            
            var resultText = status.IsLoggedIn
                ? $"✅ 已登录\n用户名: {status.Username}\n\n你可以使用其他功能了。"
                : $"❌ 未登录\n\n请使用 get_login_qrcode 工具获取二维码进行登录。";

            return new CallToolResult
            {
                Content = new List<Content>
                {
                    new TextContent { Text = resultText }
                }
            };
        });

        // 工具2: 获取登录二维码
        server.AddTool(new Tool
        {
            Name = "get_login_qrcode",
            Description = "获取登录二维码（返回Base64图片和超时时间）",
            InputSchema = new { }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: get_login_qrcode");
            var qrInfo = await _xiaoheiheService!.GetLoginQrCodeAsync();

            if (string.IsNullOrEmpty(qrInfo.QrCodeBase64))
            {
                return new CallToolResult
                {
                    Content = new List<Content>
                    {
                        new TextContent { Text = $"❌ {qrInfo.Message}" }
                    },
                    IsError = true
                };
            }

            return new CallToolResult
            {
                Content = new List<Content>
                {
                    new TextContent { Text = $"📱 {qrInfo.Message}\n过期时间: {qrInfo.ExpireTime:yyyy-MM-dd HH:mm:ss}" },
                    new ImageContent
                    {
                        Data = Convert.FromBase64String(qrInfo.QrCodeBase64),
                        MimeType = "image/png"
                    }
                }
            };
        });

        // 工具3: 发布图文内容
        server.AddTool(new Tool
        {
            Name = "publish_content",
            Description = "发布图文内容到小黑盒",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "内容标题" },
                    content = new { type = "string", description = "正文内容" },
                    images = new { type = "array", items = new { type = "string" }, description = "图片路径列表（本地绝对路径）" },
                    tags = new { type = "array", items = new { type = "string" }, description = "标签列表" }
                },
                required = new[] { "title", "content" }
            }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: publish_content");
            var publishArgs = System.Text.Json.JsonSerializer.Deserialize<PublishContentArgs>(args.ToString()!);
            var result = await _xiaoheiheService!.PublishContentAsync(publishArgs!);

            return ConvertToCallToolResult(result);
        });

        // 工具4: 搜索内容
        server.AddTool(new Tool
        {
            Name = "search_content",
            Description = "搜索小黑盒内容",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    keyword = new { type = "string", description = "搜索关键词" },
                    page = new { type = "integer", description = "页码", @default = 1 },
                    pageSize = new { type = "integer", description = "每页数量", @default = 20 }
                },
                required = new[] { "keyword" }
            }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: search_content");
            var searchArgs = System.Text.Json.JsonSerializer.Deserialize<SearchArgs>(args.ToString()!);
            var result = await _xiaoheiheService!.SearchAsync(searchArgs!);

            return ConvertToCallToolResult(result);
        });

        // 工具5: 获取帖子详情
        server.AddTool(new Tool
        {
            Name = "get_post_detail",
            Description = "获取小黑盒帖子详情",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    postId = new { type = "string", description = "帖子ID" }
                },
                required = new[] { "postId" }
            }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: get_post_detail");
            var detailArgs = System.Text.Json.JsonSerializer.Deserialize<PostDetailArgs>(args.ToString()!);
            var result = await _xiaoheiheService!.GetPostDetailAsync(detailArgs!);

            return ConvertToCallToolResult(result);
        });

        // 工具6: 发布评论
        server.AddTool(new Tool
        {
            Name = "post_comment",
            Description = "发表评论到小黑盒帖子",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    postId = new { type = "string", description = "帖子ID" },
                    content = new { type = "string", description = "评论内容" }
                },
                required = new[] { "postId", "content" }
            }
        }, async (args) =>
        {
            _logger?.LogInformation("执行工具: post_comment");
            var commentArgs = System.Text.Json.JsonSerializer.Deserialize<CommentArgs>(args.ToString()!);
            var result = await _xiaoheiheService!.PostCommentAsync(commentArgs!);

            return ConvertToCallToolResult(result);
        });

        _logger?.LogInformation("已注册 6 个MCP工具");
    }

    private static CallToolResult ConvertToCallToolResult(McpToolResult result)
    {
        var contents = new List<Content>();
        
        foreach (var c in result.Content)
        {
            if (c.Type == "text")
            {
                contents.Add(new TextContent { Text = c.Text });
            }
            else if (c.Type == "image" && !string.IsNullOrEmpty(c.Data))
            {
                contents.Add(new ImageContent
                {
                    Data = Convert.FromBase64String(c.Data),
                    MimeType = c.MimeType ?? "image/png"
                });
            }
        }

        return new CallToolResult
        {
            Content = contents,
            IsError = result.IsError
        };
    }
}
