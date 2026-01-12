using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 小黑盒服务Facade测试
/// 测试Facade协调各个专业服务的能力
/// </summary>
public class XiaoheiheServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<XiaoheiheService>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private XiaoheiheService? _service;

    public XiaoheiheServiceTests()
    {
        _loggerMock = new Mock<ILogger<XiaoheiheService>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        // 配置 LoggerFactory mock 以返回 mock logger
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // Assert
        Assert.NotNull(_service);
    }

    // 登录相关测试
    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task CheckLoginStatusAsync_ShouldDelegateToLoginService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // Act
        var result = await _service.CheckLoginStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<LoginStatus>(result);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task GetLoginQrCodeAsync_ShouldDelegateToLoginService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // Act
        var result = await _service.GetLoginQrCodeAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
    }

    // 内容发布相关测试
    [Fact]
    public async Task PublishContentAsync_ShouldDelegateToPublishService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "测试标题",
            Content = "测试内容",
            Communities = ["社区1", "社区2", "社区3"]
        };

        // Act
        var result = await _service.PublishContentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task PublishArticleAsync_ShouldDelegateToPublishService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new PublishArticleArgs
        {
            Title = "测试文章",
            Content = "测试内容",
            Tags = ["1", "2", "3", "4", "5", "6"]
        };

        // Act
        var result = await _service.PublishArticleAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task PublishVideoAsync_ShouldDelegateToPublishService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new PublishVideoArgs
        {
            Title = "测试视频",
            Description = "测试描述",
            VideoPath = "/nonexistent/video.mp4"
        };

        // Act
        var result = await _service.PublishVideoAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    // 互动相关测试
    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task SearchAsync_ShouldDelegateToInteractionService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new SearchArgs
        {
            Keyword = "测试",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.SearchAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task PostCommentAsync_ShouldDelegateToInteractionService()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new CommentArgs
        {
            PostId = "123",
            Content = "测试评论"
        };

        // Act
        var result = await _service.PostCommentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task GetPostDetailAsync_WithValidPostId_ShouldReturnDetail()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);
        var args = new PostDetailArgs
        {
            PostId = "123"
        };

        // Act
        var result = await _service.GetPostDetailAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
        {
            await _service.DisposeAsync();
        }
    }
}
