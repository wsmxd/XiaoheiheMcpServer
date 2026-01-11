using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using XiaoheiheMcpServer.Services;
using XiaoheiheMcpServer.Models;

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

// 注册服务
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<XiaoheiheService>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new XiaoheiheService(logger, loggerFactory, false);
});

// 配置MCP服务器
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<XiaoheiheMcpTools>();

await builder.Build().RunAsync();

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
        [Description("等待用户登录的超时时间（秒），默认300秒")] int waitTimeoutSeconds = 300)
    {
        logger.LogInformation("执行工具: interactive_login");
        var status = await service.InteractiveLoginAsync(waitTimeoutSeconds);
        
        return status.IsLoggedIn
            ? $"✅ {status.Message}\n用户名: {status.Username}\n\n现在可以使用其他功能了！"
            : $"❌ {status.Message}";
    }

    /// <summary>
    /// 获取登录二维码（Base64格式）- 备用方案
    /// </summary>
    [McpServerTool(Name = "get_login_qrcode")]
    [Description("获取登录二维码，扫码登录小黑盒（备用方案，推荐使用 interactive_login）")]
    public static async Task<string> GetLoginQrCode(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger)
    {
        logger.LogInformation("执行工具: get_login_qrcode");
        var qrInfo = await service.GetLoginQrCodeAsync();

        if (string.IsNullOrEmpty(qrInfo.QrCodeBase64))
        {
            return $"❌ {qrInfo.Message}";
        }

        // 返回包含Base64图片的markdown格式
        return $"📱 {qrInfo.Message}\n过期时间: {qrInfo.ExpireTime:yyyy-MM-dd HH:mm:ss}\n\n![二维码](data:image/png;base64,{qrInfo.QrCodeBase64})";
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
        [Description("社区名称列表（必须是已有的社区）")] string[]? communities = null,
        [Description("话题标签列表")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_content");
        
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
        [Description("文章正文")] string content,
        [Description("图片路径列表（本地绝对路径）")] string[]? images = null,
        [Description("标签列表")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_article");
        
        var args = new PublishArticleArgs
        {
            Title = title,
            Content = content,
            Images = images?.ToList() ?? [],
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
        [Description("视频描述")] string description,
        [Description("视频文件路径（本地绝对路径）")] string videoPath,
        [Description("封面图路径（可选，本地绝对路径）")] string? coverImagePath = null,
        [Description("标签列表（可选）")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_video");
        
        var args = new PublishVideoArgs
        {
            Title = title,
            Description = description,
            VideoPath = videoPath,
            CoverImagePath = coverImagePath,
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
        [Description("评论内容")] string content)
    {
        logger.LogInformation("执行工具: post_comment, postId={PostId}", postId);
        
        var args = new CommentArgs
        {
            PostId = postId,
            Content = content
        };
        
        var result = await service.PostCommentAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }
}
