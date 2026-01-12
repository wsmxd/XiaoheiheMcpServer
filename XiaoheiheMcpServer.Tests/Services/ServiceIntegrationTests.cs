using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Models;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 服务集成测试 - 测试多个服务协同工作
/// </summary>
public class ServiceIntegrationTests : IAsyncDisposable
{
    private readonly Mock<ILogger<XiaoheiheService>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private XiaoheiheService? _service;

    public ServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<XiaoheiheService>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        // 配置 LoggerFactory mock 以返回 mock logger
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task FullPublishingWorkflow_ShouldCoordinateAllServices()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // 1. 检查登录状态
        var loginStatus = await _service.CheckLoginStatusAsync();
        Assert.NotNull(loginStatus);

        // 2. 发布内容
        var publishArgs = new PublishContentArgs
        {
            Title = "集成测试标题",
            Content = "这是集成测试的内容",
            Images = [],
            Tags = ["测试", "集成"]
        };
        var publishResult = await _service.PublishContentAsync(publishArgs);
        Assert.NotNull(publishResult);

        // 3. 搜索内容
        var searchArgs = new SearchArgs
        {
            Keyword = "测试",
            Page = 1,
            PageSize = 20
        };
        var searchResult = await _service.SearchAsync(searchArgs);
        Assert.NotNull(searchResult);

        // Assert - 整个工作流应该完成而不抛出异常
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task MultiplePublishingMethods_ShouldAllWork()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // Act & Assert
        // 1. 发布图文
        var contentResult = await _service.PublishContentAsync(new PublishContentArgs
        {
            Title = "图文标题",
            Content = "图文内容"
        });
        Assert.NotNull(contentResult);

        // 2. 发布文章
        var articleResult = await _service.PublishArticleAsync(new PublishArticleArgs
        {
            Title = "文章标题",
            Content = "文章内容"
        });
        Assert.NotNull(articleResult);

        // 3. 发布视频（无效文件）
        var videoResult = await _service.PublishVideoAsync(new PublishVideoArgs
        {
            Title = "视频标题",
            Description = "视频描述",
            VideoPath = "/invalid/path.mp4"
        });
        Assert.NotNull(videoResult);
    }

    [Fact]
    public async Task ServiceDisposal_ShouldProperlyCleanupisSources()
    {
        // Arrange
        _service = new XiaoheiheService(_loggerMock.Object, _loggerFactoryMock.Object, headless: true);

        // Act
        await _service.DisposeAsync();

        // Assert - 应该没有异常
        Assert.NotNull(_service);
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
        {
            await _service.DisposeAsync();
        }
    }
}
