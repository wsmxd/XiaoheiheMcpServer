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

            // 1. 访问首页
            await _page!.GotoAsync(BaseUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(1000);

            // 2. 查找并点击登录按钮
            _logger.LogInformation("查找登录按钮...");
            var loginButtonSelectors = new[]
            {
                "button:has-text('登录')",
                "a:has-text('登录')",
                "[class*='login-btn']",
                "[class*='login']",
                "button.login",
                "a.login"
            };

            IElementHandle? loginButton = null;
            foreach (var selector in loginButtonSelectors)
            {
                try
                {
                    loginButton = await _page.QuerySelectorAsync(selector);
                    if (loginButton != null && await loginButton.IsVisibleAsync())
                    {
                        _logger.LogInformation($"找到登录按钮，使用选择器: {selector}");
                        break;
                    }
                }
                catch { }
            }

            if (loginButton == null)
            {
                throw new Exception("未找到登录按钮");
            }

            // 3. 点击登录按钮，弹出登录对话框
            _logger.LogInformation("点击登录按钮...");
            await loginButton.ClickAsync();
            await Task.Delay(2000); // 等待对话框弹出

            // 4. 在弹出的对话框中查找二维码
            _logger.LogInformation("在登录对话框中查找二维码...");
            var qrCodeSelectors = new[]
            {
                "img[alt*='qr']",
                "img[alt*='二维码']",
                "canvas",
                "[class*='qrcode'] img",
                "[class*='qr-code'] img",
                "[class*='qrcode'] canvas",
                "[class*='qr-code'] canvas",
                "img[src*='qrcode']",
                "img[src*='qr']"
            };

            IElementHandle? qrCodeElement = null;
            foreach (var selector in qrCodeSelectors)
            {
                try
                {
                    var elements = await _page.QuerySelectorAllAsync(selector);
                    foreach (var element in elements)
                    {
                        if (await element.IsVisibleAsync())
                        {
                            qrCodeElement = element;
                            _logger.LogInformation($"找到二维码元素，使用选择器: {selector}");
                            break;
                        }
                    }
                    if (qrCodeElement != null) break;
                }
                catch { }
            }

            if (qrCodeElement == null)
            {
                throw new Exception("未找到二维码元素，可能登录对话框未正确弹出");
            }

            // 5. 获取二维码图片
            string qrCodeBase64;
            var src = await qrCodeElement.GetAttributeAsync("src");
            
            if (!string.IsNullOrEmpty(src) && src.StartsWith("data:image"))
            {
                // Base64 编码的图片
                qrCodeBase64 = src.Split(',')[1];
                _logger.LogInformation("二维码是 Base64 格式");
            }
            else if (!string.IsNullOrEmpty(src))
            {
                // URL 图片，需要下载
                _logger.LogInformation($"二维码是 URL 格式: {src}");
                qrCodeBase64 = await DownloadImageAsBase64(src);
            }
            else
            {
                // Canvas 或其他元素，截图获取
                _logger.LogInformation("使用截图方式获取二维码");
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
