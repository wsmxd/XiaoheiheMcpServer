using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 发布服务测试
/// </summary>
public class PublishServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<PublishService>> _loggerMock;
    private PublishService? _service;

    public PublishServiceTests()
    {
        _loggerMock = new Mock<ILogger<PublishService>>();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        _service = new PublishService(_loggerMock.Object, headless: true);

        // Assert
        Assert.NotNull(_service);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task PublishContentAsync_WithValidArgs_ShouldReturnResult()
    {
        // Arrange
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "测试标题",
            Content = "测试内容",
            Images = [],
            Tags = ["测试"]
        };

        // Act
        var result = await _service.PublishContentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content);
        Assert.True(result.Content[0].Type == "text");
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task PublishContentAsync_WithEmptyTitle_ShouldHandleGracefully()
    {
        // Arrange
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "",
            Content = "测试内容",
            Images = [],
            Tags = []
        };

        // Act
        var result = await _service.PublishContentAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task PublishContentAsync_WithTooManyCommunities_ShouldReturnError()
    {
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "测试标题",
            Content = "测试内容",
            Images = [],
            Communities = ["社区1", "社区2", "社区3"],
            Tags = []
        };

        var result = await _service.PublishContentAsync(args);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.NotNull(result.Content);
        Assert.Contains("communities", result.Content[0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishContentAsync_WithTooManyTags_ShouldReturnError()
    {
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishContentArgs
        {
            Title = "测试标题",
            Content = "测试内容",
            Images = [],
            Tags = ["1", "2", "3", "4", "5", "6"]
        };

        var result = await _service.PublishContentAsync(args);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.NotNull(result.Content);
        Assert.Contains("tags", result.Content[0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task PublishArticleAsync_WithValidArgs_ShouldReturnResult()
    {
        // Arrange
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishArticleArgs
        {
            Title = "测试文章",
            Content = "这是一篇测试文章内容",
            Images = [],
            Tags = ["测试", "文章"]
        };

        // Act
        var result = await _service.PublishArticleAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content);
    }

    [Fact]
    public async Task PublishVideoAsync_WithNonExistentFile_ShouldReturnError()
    {
        // Arrange
        _service = new PublishService(_loggerMock.Object, headless: true);
        var args = new PublishVideoArgs
        {
            Title = "测试视频",
            Description = "测试描述",
            VideoPath = "/nonexistent/video.mp4",
            Tags = []
        };

        // Act
        var result = await _service.PublishVideoAsync(args);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
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
