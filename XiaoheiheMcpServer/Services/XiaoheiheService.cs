using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 小黑盒服务Facade - 协调各个专业服务
/// 使用Facade模式简化客户端的交互
/// </summary>
public class XiaoheiheService : IAsyncDisposable
{
    private readonly LoginService _loginService;
    private readonly PublishService _publishService;
    private readonly ArticlePublishService _articlePublishService;
    private readonly InteractionService _interactionService;
    private readonly ILogger<XiaoheiheService> _logger;

    public XiaoheiheService(
        ILogger<XiaoheiheService> logger,
        ILoggerFactory loggerFactory,
        bool headless = true)
    {
        _logger = logger;
        // 使用 ILoggerFactory 为各具体服务创建类型化 Logger，避免不安全的类型转换
        _loginService = new LoginService(loggerFactory.CreateLogger<LoginService>(), headless);
        _publishService = new PublishService(loggerFactory.CreateLogger<PublishService>(), headless);
        _articlePublishService = new ArticlePublishService(loggerFactory.CreateLogger<ArticlePublishService>(), headless);
        _interactionService = new InteractionService(loggerFactory.CreateLogger<InteractionService>(), headless);
    }

    #region 登录相关

    /// <summary>
    /// 检查登录状态
    /// </summary>
    public Task<LoginStatus> CheckLoginStatusAsync()
    {
        _logger.LogInformation("调用登录状态检查服务");
        return _loginService.CheckLoginStatusAsync();
    }

    /// <summary>
    /// 交互式登录 - 打开浏览器让用户手动登录
    /// </summary>
    public Task<LoginStatus> InteractiveLoginAsync(int waitTimeoutSeconds = 300)
    {
        _logger.LogInformation("调用交互式登录服务");
        return _loginService.InteractiveLoginAsync(waitTimeoutSeconds);
    }

    /// <summary>
    /// 获取登录二维码（备用方案）
    /// </summary>
    public Task<QrCodeInfo> GetLoginQrCodeAsync()
    {
        _logger.LogInformation("调用登录二维码获取服务（备用方案）");
        return _loginService.GetLoginQrCodeAsync();
    }

    #endregion

    #region 内容发布相关

    /// <summary>
    /// 发布图文内容
    /// </summary>
    public Task<McpToolResult> PublishContentAsync(PublishContentArgs args)
    {
        _logger.LogInformation($"调用发布内容服务: {args.Title}");
        return _publishService.PublishContentAsync(args);
    }

    /// <summary>
    /// 发布文章
    /// </summary>
    public Task<McpToolResult> PublishArticleAsync(PublishArticleArgs args)
    {
        _logger.LogInformation($"调用发布文章服务: {args.Title}");
        return _articlePublishService.PublishArticleAsync(args);
    }

    /// <summary>
    /// 发布视频
    /// </summary>
    public Task<McpToolResult> PublishVideoAsync(PublishVideoArgs args)
    {
        _logger.LogInformation($"调用发布视频服务: {args.Title}");
        return _publishService.PublishVideoAsync(args);
    }

    #endregion

    #region 互动相关

    /// <summary>
    /// 发布评论
    /// </summary>
    public Task<McpToolResult> PostCommentAsync(CommentArgs args)
    {
        _logger.LogInformation($"调用发布评论服务: {args.PostId}");
        return _interactionService.PostCommentAsync(args);
    }

    /// <summary>
    /// 搜索内容
    /// </summary>
    public Task<McpToolResult> SearchAsync(SearchArgs args)
    {
        _logger.LogInformation($"调用搜索服务: {args.Keyword}");
        return _interactionService.SearchAsync(args);
    }

    /// <summary>
    /// 获取帖子详情
    /// </summary>
    public Task<McpToolResult> GetPostDetailAsync(PostDetailArgs args)
    {
        _logger.LogInformation($"调用获取帖子详情服务: {args.PostId}");
        return _interactionService.GetPostDetailAsync(args);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("释放小黑盒服务资源");
        await _loginService.DisposeAsync();
        await _publishService.DisposeAsync();
        await _interactionService.DisposeAsync();
        await _articlePublishService.DisposeAsync();
    }
}

