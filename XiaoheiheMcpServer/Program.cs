using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using XiaoheiheMcpServer.Services;
using XiaoheiheMcpServer.Models;

var builder = Host.CreateApplicationBuilder(args);

// 配置日志输出到stderr（MCP规范要求）
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// 解析命令行参数 - 是否使用无头模式
var headless = !args.Contains("--no-headless");

// 注册服务
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<XiaoheiheService>>();
    return new XiaoheiheService(logger, headless);
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
            : $"❌ 未登录\n\n请使用 get_login_qrcode 工具获取二维码进行登录。";
    }

    /// <summary>
    /// 获取登录二维码（Base64格式）
    /// </summary>
    [McpServerTool(Name = "get_login_qrcode")]
    [Description("获取登录二维码，扫码登录小黑盒")]
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
        [Description("标签列表")] string[]? tags = null)
    {
        logger.LogInformation("执行工具: publish_content");
        
        var args = new PublishContentArgs
        {
            Title = title,
            Content = content,
            Images = images?.ToList() ?? [],
            Tags = tags?.ToList() ?? []
        };
        
        var result = await service.PublishContentAsync(args);
        
        return string.Join("\n", result.Content.Select(c => c.Text));
    }

    /// <summary>
    /// 搜索小黑盒内容
    /// </summary>
    [McpServerTool(Name = "search_content")]
    [Description("搜索小黑盒内容")]
    public static async Task<string> SearchContent(
        XiaoheiheService service,
        ILogger<XiaoheiheMcpTools> logger,
        [Description("搜索关键词")] string keyword,
        [Description("页码")] int page = 1,
        [Description("每页数量")] int pageSize = 20)
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
