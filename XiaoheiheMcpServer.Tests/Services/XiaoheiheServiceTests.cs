using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 小黑盒服务测试
/// </summary>
public class XiaoheiheServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<XiaoheiheService>> _loggerMock;
    private XiaoheiheService? _service;

    public XiaoheiheServiceTests()
    {
        _loggerMock = new Mock<ILogger<XiaoheiheService>>();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);

        // Assert
        Assert.NotNull(_service);
    }

    [Fact]
    public async Task CheckLoginStatusAsync_ShouldReturnLoginStatus()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);

        // Act
        var result = await _service.CheckLoginStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<LoginStatus>(result);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    [Fact]
    public async Task PublishContentAsync_WithEmptyTitle_ShouldHandleGracefully()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "",
            Content = "测试内容"
        };

        // Act
        var result = await _service.PublishContentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task SearchAsync_WithValidKeyword_ShouldReturnResults()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);
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
        Assert.Single(result.Content);
    }

    [Fact]
    public async Task PostCommentAsync_WithValidArgs_ShouldReturnResult()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);
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

    [Fact]
    public async Task GetPostDetailAsync_WithValidPostId_ShouldReturnDetail()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, headless: true);
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
