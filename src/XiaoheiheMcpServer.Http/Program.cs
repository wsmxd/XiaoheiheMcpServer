using ModelContextProtocol.Server;
using System.ComponentModel;
using XiaoheiheMcpServer.Http;
using XiaoheiheMcpServer.Shared.Models;
using XiaoheiheMcpServer.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

var headless = !args.Contains("--show-browser");

// 注册 XianheiheService 为单例
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<XiaoheiheService>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new XiaoheiheService(logger, loggerFactory, headless);
});

// 配置 MCP 服务器 - 使用官方 ASP.NET Core 传输
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly();

// 配置 CORS
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

// 映射 MCP 端点
app.MapMcp();

// 根路径 - 服务器信息
app.MapGet("/", () => new
{
    name = "小黑盒MCP HTTP服务器",
    version = "2.0.0",
    protocol = "MCP over HTTP (Streamable HTTP)",
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

// 在应用停止时清理资源
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(async () =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("正在关闭HTTP服务器，清理资源...");
    await BrowserBase.CleanupSharedBrowserAsync();
});

app.Run();


/// <summary>
/// 小黑盒MCP工具集
/// </summary>
[McpServerToolType]
public class XiaoheiheMcpTools
{
    /// <summary>
    /// 检查小黑盒登录状态
    /// </summary>
    [McpServerTool(Name = "check_login_status"), Description("检查小黑盒登录状态")]
    public async Task<string> CheckLoginStatus(
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
    [McpServerTool(Name = "interactive_login"), Description("打开浏览器窗口，让用户手动登录小黑盒（推荐首次登录使用）")]
    public async Task<string> InteractiveLogin(
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
    /// 获取登录二维码
    /// </summary>
    [McpServerTool(Name = "get_login_qr_code"), Description("获取登录二维码然后自动打开图片让用户扫描，返回登录的结果（适合无头模式）")]
    public async Task<QrCodeInfo> GetLoginQrCode(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger)
    {
        logger.LogInformation("执行工具: get_login_qr_code");
        var qrCodeInfo = await service.GetLoginQrCodeAsync();
        return qrCodeInfo;
    }

    /// <summary>
    /// 获取用户个人信息
    /// </summary>
    [McpServerTool(Name = "get_user_profile"), Description("获取用户个人信息（需要登录状态）")]
    public async Task<object> GetUserProfile(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("每页条数，默认10")] int pageSize = 10)
    {
        logger.LogInformation("执行工具: get_user_profile");
        return await service.GetUserProfileAsync(pageSize);
    }

    /// <summary>
    /// 发布图文内容到小黑盒
    /// </summary>
    [McpServerTool(Name = "publish_content"), Description("发布图文内容到小黑盒")]
    public async Task<string> PublishContent(
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
            return "❌ communities 最多只能传 2 个";

        if (tags is { Length: > 5 })
            return "❌ tags 最多只能传 5 个";

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
    [McpServerTool(Name = "publish_article"), Description("发布文章到小黑盒（适合长文章）")]
    public async Task<string> PublishArticle(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("文章标题")] string title,
        [Description("文章正文（可包含绝对图片路径，将自动识别并上传）")] string content,
        [Description("社区名称列表（必须是已有社区，最多2个）")] string[] communities,
        [Description("话题标签列表（可选，最多5个）")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_article");

        if (communities.Length > 2)
            return "❌ communities 最多只能传 2 个";

        if (tags is { Length: > 5 })
            return "❌ tags 最多只能传 5 个";

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
    [McpServerTool(Name = "publish_video"), Description("发布视频到小黑盒")]
    public async Task<string> PublishVideo(
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
            return "❌ communities 最多只能传 2 个";

        if (tags is { Length: > 5 })
            return "❌ tags 最多只能传 5 个";

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
    /// 获取小黑盒首页内容
    /// </summary>
    [McpServerTool(Name = "get_home_content"), Description("获取小黑盒首页内容")]
    public async Task<List<SearchResultItem>> GetHomeContent(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger)
    {
        logger.LogInformation("执行工具: get_home_content");
        return await service.GetHomeContentAsync();
    }

    /// <summary>
    /// 搜索小黑盒内容
    /// </summary>
    [McpServerTool(Name = "search_content"), Description("搜索小黑盒内容，返回匹配的帖子列表")]
    public async Task<string> SearchContent(
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

        var result = await service.SearchContentAsync(args);
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 获取小黑盒帖子详情
    /// </summary>
    [McpServerTool(Name = "get_post_detail"), Description("获取小黑盒帖子详情")]
    public async Task<PostDetail> GetPostDetail(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("帖子ID")] string postId)
    {
        logger.LogInformation("执行工具: get_post_detail, postId={PostId}", postId);

        var args = new PostDetailArgs
        {
            PostId = postId
        };

        return await service.GetPostDetailAsync(args);
    }

    /// <summary>
    /// 发表评论到小黑盒帖子
    /// </summary>
    [McpServerTool(Name = "post_comment"), Description("发表评论到小黑盒帖子")]
    public async Task<string> PostComment(
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

    /// <summary>
    /// 回复小黑盒帖子下的评论
    /// </summary>
    [McpServerTool(Name = "reply_comment"), Description("回复小黑盒帖子下的评论")]
    public async Task<string> ReplyComment(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("帖子ID")] string postId,
        [Description("要回复的目标评论内容")] string targetCommentContent,
        [Description("回复内容")] string content)
    {
        logger.LogInformation("执行工具: reply_comment, postId={PostId}", postId);

        var args = new ReplyCommentArgs
        {
            PostId = postId,
            TargetCommentContent = targetCommentContent,
            Content = content
        };

        var result = await service.ReplyCommentAsync(args);
        return string.Join("\n", result.Content.Select(c => c.Text));
    }
}
