using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 小黑盒登录管理服务
/// </summary>
public class LoginService : BrowserBase
{
    public LoginService(ILogger<LoginService> logger, bool headless = true) 
        : base(logger, headless)
    {
    }

    /// <summary>
    /// 检查登录状态
    /// </summary>
    public async Task<LoginStatus> CheckLoginStatusAsync()
    {
        try
        {
            _logger.LogInformation("检查登录状态...");
            await InitializeBrowserAsync();
            
            await _page!.GotoAsync(BaseUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // 检查是否有用户头像或用户信息元素
            var userElement = await _page.QuerySelectorAsync("[class*='user'], [class*='avatar'], [class*='login-user']");
            var isLoggedIn = userElement != null;

            if (isLoggedIn)
            {
                var username = await userElement!.TextContentAsync() ?? "xiaoheihe-user";
                _logger.LogInformation($"已登录: {username}");
                return new LoginStatus
                {
                    IsLoggedIn = true,
                    Username = username.Trim(),
                    Message = "已登录"
                };
            }

            _logger.LogInformation("未登录");
            return new LoginStatus
            {
                IsLoggedIn = false,
                Message = "未登录，请使用二维码登录"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查登录状态失败");
            return new LoginStatus
            {
                IsLoggedIn = false,
                Message = $"检查登录状态失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取登录二维码
    /// </summary>
    public async Task<QrCodeInfo> GetLoginQrCodeAsync()
    {
        try
        {
            _logger.LogInformation("获取登录二维码...");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/account/login");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 查找二维码元素
            var qrCodeElement = await _page.QuerySelectorAsync("img[alt*='qr'], img[alt*='二维码'], canvas, [class*='qrcode'], [class*='qr-code']") 
                ?? throw new Exception("未找到二维码元素");
            
            string qrCodeBase64;
            var src = await qrCodeElement.GetAttributeAsync("src");
            
            if (!string.IsNullOrEmpty(src) && src.StartsWith("data:image"))
            {
                qrCodeBase64 = src.Split(',')[1];
            }
            else if (!string.IsNullOrEmpty(src))
            {
                qrCodeBase64 = await DownloadImageAsBase64(src);
            }
            else
            {
                var screenshot = await qrCodeElement.ScreenshotAsync();
                qrCodeBase64 = Convert.ToBase64String(screenshot);
            }

            _logger.LogInformation("二维码获取成功，等待扫码登录...");
            await WaitForLoginAsync();

            return new QrCodeInfo
            {
                QrCodeBase64 = qrCodeBase64,
                ExpireTime = DateTime.Now.AddMinutes(5),
                Message = "请使用小黑盒APP扫描二维码登录"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取登录二维码失败");
            return new QrCodeInfo
            {
                Message = $"获取登录二维码失败: {ex.Message}"
            };
        }
    }
}
