using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using XiaoheiheMcpServer.Services;
using XiaoheiheMcpServer.Shared.Models;

var builder = Host.CreateApplicationBuilder(args);

// 配置日志输出到stderr（MCP规范要求）
// 禁用Console日志，只在stderr上输出
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning); // 只输出警告及以上

// 解析命令行参数 - 是否使用无头模式
var headless = !args.Contains("--no-headless");

// 检查是否首次使用（是否存在 Cookie 文件）
var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
var cookiePath = Path.Combine(dataDir, "cookies.json");
var isFirstTime = !File.Exists(cookiePath);

// 首次使用时强制使用有头模式，让用户能看到浏览器完成登录
if (isFirstTime)
{
    headless = false;
    Console.Error.WriteLine("🔔 检测到首次使用，将使用有头模式打开浏览器，请完成登录后后续将自动使用无头模式");
}

// 注册服务
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<XiaoheiheService>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new XiaoheiheService(logger, loggerFactory, headless);
});

// 配置MCP服务器
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<XiaoheiheMcpTools>();

var host = builder.Build();

// 监听应用停止事件，手动释放 XiaoheiheService 资源
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(async () =>
{
    Console.Error.WriteLine("正在关闭服务器，清理资源...");
    var service = host.Services.GetService<XiaoheiheService>();
    if (service != null)
    {
        await service.DisposeAsync();
    }
});

await host.RunAsync();

/// <summary>
/// 小黑盒MCP工具集
/// </summary>
[McpServerToolType]
file class XiaoheiheMcpTools
{
    /// <summary>
    /// 检查小黑盒登录状态
    /// </summary>
    [McpServerTool(Name = "check_login_status")]
    [Description("检查小黑盒登录状态")]
    public static async Task<string> CheckLoginStatus(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger)
    {
        logger.LogInformation("执行工具: check_login_status");
        var status = await service.CheckLoginStatusAsync();
        
        return status.IsLoggedIn
            ? $"✅ 已登录\n用户名: {status.Username}\n\n你可以使用其他功能了。"
            : $"❌ 未登录\n\n{status.Message}\n\n推荐使用 interactive_login 工具进行首次登录（打开浏览器手动登录），或使用 get_login_qrcode 获取二维码。";
    }

    /// <summary>
    /// 交互式登录 - 打开浏览器让用户手动登录
    /// </summary>
    [McpServerTool(Name = "interactive_login")]
    [Description("打开浏览器窗口，让用户手动登录小黑盒（推荐首次登录使用）")]
    public static async Task<string> InteractiveLogin(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("等待用户登录的超时时间（秒），默认180秒")] int waitTimeoutSeconds = 180)
    {
        logger.LogInformation("执行工具: interactive_login");
        var status = await service.InteractiveLoginAsync(waitTimeoutSeconds);
        
        return status.IsLoggedIn
            ? $"✅ {status.Message}\n用户名: {status.Username}\n\n现在可以使用其他功能了！"
            : $"❌ {status.Message}";
    }

    /// <summary>
    /// 发布图文内容到小黑盒
    /// </summary>
    [McpServerTool(Name = "publish_content")]
    [Description("发布图文内容到小黑盒")]
    public static async Task<string> PublishContent(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("内容标题")] string title,
        [Description("正文内容")] string content,
        [Description("图片路径列表（本地绝对路径）")] string[]? images = null,
        [Description("社区名称列表（必须是已有的社区，最多2个）")] string[]? communities = null,
        [Description("话题标签列表（最多5个）")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_content");

        if (communities is { Length: > 2 })
        {
            return "❌ communities 最多只能传 2 个";
        }

        if (tags is { Length: > 5 })
        {
            return "❌ tags 最多只能传 5 个";
        }
        
        var args = new PublishContentArgs
        {
            Title = title,
            Content = content,
            Images = images?.ToList() ?? [],
            Communities = communities?.ToList() ?? [],
            Tags = tags?.ToList() ?? []
        };
        
        var result = await service.PublishContentAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 发布文章到小黑盒（长文章形式）
    /// </summary>
    [McpServerTool(Name = "publish_article")]
    [Description("发布文章到小黑盒（适合长文章）")]
    public static async Task<string> PublishArticle(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("文章标题")] string title,
        [Description("文章正文（可包含绝对图片路径，将自动识别并上传）")] string content,
        [Description("社区名称列表（必须是已有社区，最多2个）")] string[] communities,
        [Description("话题标签列表（可选，最多5个）")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_article");

        if (communities.Length > 2)
        {
            return "❌ communities 最多只能传 2 个";
        }

        if (tags is { Length: > 5 })
        {
            return "❌ tags 最多只能传 5 个";
        }
        
        var args = new PublishArticleArgs
        {
            Title = title,
            Content = content,
            Communities = [.. communities],
            Tags = tags?.ToList() ?? []
        };
        
        var result = await service.PublishArticleAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 发布视频到小黑盒
    /// </summary>
    [McpServerTool(Name = "publish_video")]
    [Description("发布视频到小黑盒")]
    public static async Task<string> PublishVideo(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("视频标题")] string title,
        [Description("视频正文/描述")] string content,
        [Description("视频文件路径（本地绝对路径）")] string videoPath,
        [Description("视频封面图路径（本地绝对路径）")] string coverImagePath,
        [Description("社区名称列表（必须是已有的社区，最多2个）")] string[]? communities = null,
        [Description("话题标签列表（最多5个）")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_video");

        if (communities is { Length: > 2 })
        {
            return "❌ communities 最多只能传 2 个";
        }

        if (tags is { Length: > 5 })
        {
            return "❌ tags 最多只能传 5 个";
        }
        
        var args = new PublishVideoArgs
        {
            Title = title,
            Content = content,
            VideoPath = videoPath,
            CoverImagePath = coverImagePath,
            Communities = communities?.ToList() ?? [],
            Tags = tags?.ToList() ?? []
        };
        
        var result = await service.PublishVideoAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 搜索小黑盒内容
    /// </summary>
    [McpServerTool(Name = "search_content")]
    [Description("搜索小黑盒内容，返回匹配的帖子列表")]
    public static async Task<string> SearchContent(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("搜索关键词")] string keyword,
        [Description("页码，默认1")] int page = 1,
        [Description("每页数量，默认20，最多20条")] int pageSize = 20)
    {
        logger.LogInformation("执行工具: search_content, keyword={Keyword}", keyword);
        
        var args = new SearchArgs
        {
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };
        
        var result = await service.SearchAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 获取小黑盒帖子详情
    /// </summary>
    [McpServerTool(Name = "get_post_detail")]
    [Description("获取小黑盒帖子详情")]
    public static async Task<string> GetPostDetail(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("帖子ID")] string postId)
    {
        logger.LogInformation("执行工具: get_post_detail, postId={PostId}", postId);
        
        var args = new PostDetailArgs
        {
            PostId = postId
        };
        
        var result = await service.GetPostDetailAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 发表评论到小黑盒帖子
    /// </summary>
    [McpServerTool(Name = "post_comment")]
    [Description("发表评论到小黑盒帖子")]
    public static async Task<string> PostComment(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("帖子ID")] string postId,
        [Description("评论内容")] string content,
        [Description("评论图片路径列表（可选，本地绝对路径）")] string[]? images = null)
    {
        logger.LogInformation("执行工具: post_comment, postId={PostId}", postId);
        
        var args = new CommentArgs
        {
            PostId = postId,
            Content = content,
            Images = images?.ToList() ?? []
        };
        
        var result = await service.PostCommentAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }
}

