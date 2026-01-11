using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 互动服务测试
/// </summary>
public class InteractionServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<InteractionService>> _loggerMock;
    private InteractionService? _service;

    public InteractionServiceTests()
    {
        _loggerMock = new Mock<ILogger<InteractionService>>();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        _service = new InteractionService(_loggerMock.Object, headless: true);

        // Assert
        Assert.NotNull(_service);
    }

    [Fact]
    public async Task SearchAsync_WithValidKeyword_ShouldReturnResults()
    {
        // Arrange
        _service = new InteractionService(_loggerMock.Object, headless: true);
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
        Assert.True(result.Content[0].Type == "text");
    }

    [Fact]
    public async Task PostCommentAsync_WithValidArgs_ShouldReturnResult()
    {
        // Arrange
        _service = new InteractionService(_loggerMock.Object, headless: true);
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
        Assert.Single(result.Content);
    }

    [Fact]
    public async Task PostCommentAsync_WithEmptyContent_ShouldHandleGracefully()
    {
        // Arrange
        _service = new InteractionService(_loggerMock.Object, headless: true);
        var args = new CommentArgs
        {
            PostId = "123",
            Content = ""
        };

        // Act
        var result = await _service.PostCommentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task GetPostDetailAsync_WithValidPostId_ShouldReturnPostDetail()
    {
        // Arrange
        _service = new InteractionService(_loggerMock.Object, headless: true);
        var args = new PostDetailArgs
        {
            PostId = "123"
        };

        // Act
        var result = await _service.GetPostDetailAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content);
        Assert.True(result.Content[0].Type == "text");
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
        {
            await _service.DisposeAsync();
        }
    }
}
