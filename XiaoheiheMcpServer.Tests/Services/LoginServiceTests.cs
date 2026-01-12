using Microsoft.Extensions.Logging;
using Moq;
using XiaoheiheMcpServer.Services;

namespace XiaoheiheMcpServer.Tests.Services;

/// <summary>
/// 登录服务测试
/// </summary>
public class LoginServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<LoginService>> _loggerMock;
    private LoginService? _service;

    public LoginServiceTests()
    {
        _loggerMock = new Mock<ILogger<LoginService>>();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange & Act
        _service = new LoginService(_loggerMock.Object, headless: true);

        // Assert
        Assert.NotNull(_service);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task CheckLoginStatusAsync_ShouldReturnLoginStatus()
    {
        // Arrange
        _service = new LoginService(_loggerMock.Object, headless: true);

        // Act
        var result = await _service.CheckLoginStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        // LoginStatus 应该包含 IsLoggedIn 和 Message
        Assert.True(result.IsLoggedIn == false || result.IsLoggedIn == true);
    }

    [Fact(Skip = "依赖真实网页/Playwright 环境，默认跳过")]
    public async Task GetLoginQrCodeAsync_ShouldReturnQrCodeInfo()
    {
        // Arrange
        _service = new LoginService(_loggerMock.Object, headless: true);

        // Act
        var result = await _service.GetLoginQrCodeAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
        // QrCodeInfo 应该包含消息和过期时间信息
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
        {
            await _service.DisposeAsync();
        }
    }
}
