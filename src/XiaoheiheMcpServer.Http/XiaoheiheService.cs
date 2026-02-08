using XiaoheiheMcpServer.Shared.Models;
using XiaoheiheMcpServer.Shared.Services;

namespace XiaoheiheMcpServer.Http;

public class XiaoheiheService : IAsyncDisposable
{
    private readonly LoginService _loginService;
    private readonly PublishService _publishService;
    private readonly ArticlePublishService _articlePublishService;
    private readonly InteractionService _interactionService;
    private readonly UserProfileService _userProfileService;
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
        _userProfileService = new UserProfileService(loggerFactory.CreateLogger<UserProfileService>(), headless);
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
    /// 用户自行进行登录(需要使用有头模式启动)
    /// </summary>
    public async Task<LoginStatus> InteractiveLoginAsync(int waitTimeoutSeconds = 300)
    {
        _logger.LogInformation("调用交互式登录服务");
        return await _loginService.InteractiveLoginAsync(waitTimeoutSeconds);
    }
    /// <summary> 获取登录二维码
    /// </summary> 
    /// <returns>二维码信息，包括二维码数据和过期时间</returns>
    public Task<QrCodeInfo> GetLoginQrCodeAsync()
    {
        _logger.LogInformation("调用登录二维码获取服务");
        return _loginService.GetLoginQrCodeAsync();
    }
    #endregion

    #region 发布相关
    /// <summary>
    /// 发布图文内容
    /// </summary>
    public Task<McpToolResult> PublishContentAsync(PublishContentArgs options)
    {
        _logger.LogInformation("调用内容发布服务");
        return _publishService.PublishContentAsync(options);
    }

    /// <summary>
    /// 发布文章内容
    /// </summary>
    public Task<McpToolResult> PublishArticleAsync(PublishArticleArgs options)
    {
        _logger.LogInformation("调用文章发布服务");
        return _articlePublishService.PublishArticleAsync(options);
    }

    /// <summary>
    /// 发布视频内容
    /// </summary>
    /// <returns>McpToolResult</returns>
    public Task<McpToolResult> PublishVideoAsync(PublishVideoArgs options)
    {
        _logger.LogInformation("调用视频发布服务");
        return _publishService.PublishVideoAsync(options);
    }
    #endregion

    #region 互动相关
    /// <summary>
    /// 获取首页内容
    /// </summary>
    public Task<List<SearchResultItem>> GetHomeContentAsync()
    {
        _logger.LogInformation("调用首页内容获取服务");
        return _interactionService.GetHomePostsAsync();
    }

    /// <summary>
    /// 搜索内容
    /// </summary>
    public Task<McpToolResult> SearchContentAsync(SearchArgs args)
    {
        _logger.LogInformation("调用内容搜索服务");
        return _interactionService.SearchAsync(args);
    }

    /// <summary>
    /// 获取具体帖子的内容
    /// <paramref name="postId"/>
    /// </summary>
    public Task<McpToolResult> GetPostDetailAsync(PostDetailArgs postId)
    {
        _logger.LogInformation("调用帖子内容获取服务");
        return _interactionService.GetPostDetailAsync(postId);
    }

    /// <summary>
    /// 发布评论
    /// </summary>
    public Task<McpToolResult> PostCommentAsync(CommentArgs args)
    {
        _logger.LogInformation("调用评论发布服务");
        return _interactionService.PostCommentAsync(args);
    }
    #endregion

    #region 个人信息相关
    /// <summary>
    /// 获取用户个人信息（需要登录状态）
    /// </summary> <returns>用户的动态</returns>
    public Task<object> GetUserProfileAsync(int pageSize = 10)
    {
        _logger.LogInformation("调用用户个人信息获取服务");
        return _userProfileService.GetUserProfileAsync(pageSize);
    }
    #endregion

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}