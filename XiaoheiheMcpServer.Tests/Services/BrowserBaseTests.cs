using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 浏览器基础服务测试
/// </summary>
public class BrowserBaseTests
{
    /// <summary>
    /// 测试具体实现类来验证BrowserBase功能
    /// </summary>
    private class TestBrowserService : BrowserBase
    {
        public TestBrowserService(ILogger logger, bool headless = true)
            : base(logger, headless)
        {
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        var service = new TestBrowserService(loggerMock.Object, headless: true);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_ShouldCreateDataDirectory()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

        // Act
        var service = new TestBrowserService(loggerMock.Object, headless: true);

        // Assert
        Assert.True(Directory.Exists(dataDir));
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();
        var service = new TestBrowserService(loggerMock.Object, headless: true);

        // Act & Assert
        await service.DisposeAsync();
    }
}
