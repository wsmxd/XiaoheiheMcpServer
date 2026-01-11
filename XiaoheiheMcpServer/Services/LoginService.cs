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
            
            // 访问首页
            const string checkUrl = "https://www.xiaoheihe.cn/app/bbs/home";
            _logger.LogInformation("访问首页: {Url}", checkUrl);
            await _page!.GotoAsync(checkUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(1000);

            // 查找用户信息元素 p.user-box__username
            var userElement = await _page.QuerySelectorAsync("p.user-box__username");
            
            if (userElement != null)
            {
                var username = await userElement.TextContentAsync();
                username = (username ?? "xiaoheihe-user").Trim();
                _logger.LogInformation("已登录，用户: {Username}", username);
                return new LoginStatus
                {
                    IsLoggedIn = true,
                    Username = username,
                    Message = "已登录"
                };
            }

            _logger.LogInformation("未找到用户信息，判断为未登录");
            return new LoginStatus
            {
                IsLoggedIn = false,
                Message = "未登录，请使用 interactive_login 工具进行登录"
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
    /// 交互式登录 - 打开有头浏览器让用户手动登录
    /// </summary>
    /// <param name="waitTimeoutSeconds">等待用户登录的超时时间（秒），默认300秒（5分钟）</param>
    public async Task<LoginStatus> InteractiveLoginAsync(int waitTimeoutSeconds = 300)
    {
        try
        {
            _logger.LogInformation("启动交互式登录，等待用户手动登录...");
            
            // 确保使用有头模式
            if (_headless)
            {
                _logger.LogWarning("交互式登录需要有头模式，正在重新初始化...");
                // 释放现有浏览器
                if (_browser != null) await _browser.CloseAsync();
                _browser = null;
            }

            await InitializeBrowserAsync();

            // 访问登录页面
            var loginUrl = "https://login.xiaoheihe.cn/?origin=heybox&redirect_url=https%3A%2F%2Fwww.xiaoheihe.cn%2Fhome";
            _logger.LogInformation("打开登录页面: {Url}", loginUrl);
            await _page!.GotoAsync(loginUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _logger.LogInformation("请在浏览器中完成登录操作（手机验证码、密码或扫码均可）");
            _logger.LogInformation("等待最多 {Timeout} 秒...", waitTimeoutSeconds);

            // 等待登录成功（通过URL跳转或特定元素出现判断）
            try
            {
                await _page.WaitForFunctionAsync(
                    @"() => {
                        // 检查是否跳转到首页
                        if (window.location.href.includes('xiaoheihe.cn/home') || 
                            window.location.href.includes('xiaoheihe.cn') && !window.location.href.includes('login')) {
                            return true;
                        }
                        // 或者检查是否出现用户信息元素
                        const userElement = document.querySelector('[class*=""user""], [class*=""avatar""]');
                        return !!userElement;
                    }",
                    new PageWaitForFunctionOptions { Timeout = waitTimeoutSeconds * 1000 }
                );

                _logger.LogInformation("检测到登录成功！");
                
                // 保存 Cookie
                await SaveCookiesAsync();
                
                // 获取用户信息
                await Task.Delay(2000); // 等待页面稳定
                var username = await _page.EvaluateAsync<string>(
                    @"() => {
                        const userElement = document.querySelector('[class*=""user""], [class*=""avatar""], [class*=""username""]');
                        return userElement?.textContent?.trim() || 'xiaoheihe-user';
                    }"
                );

                return new LoginStatus
                {
                    IsLoggedIn = true,
                    Username = username,
                    Message = "登录成功！Cookie 已保存，后续操作将自动使用此登录状态"
                };
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("等待登录超时");
                return new LoginStatus
                {
                    IsLoggedIn = false,
                    Message = $"登录超时（{waitTimeoutSeconds}秒内未完成登录）"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "交互式登录失败");
            return new LoginStatus
            {
                IsLoggedIn = false,
                Message = $"交互式登录失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取登录二维码（备用方案）
    /// </summary>
    public async Task<QrCodeInfo> GetLoginQrCodeAsync()
    {
        try
        {
            _logger.LogInformation("获取登录二维码（备用方案，推荐使用 interactive_login）...");
            await InitializeBrowserAsync();

            // 直接访问登录页面
            var loginUrl = "https://login.xiaoheihe.cn/?origin=heybox&redirect_url=https%3A%2F%2Fwww.xiaoheihe.cn%2Fhome";
            _logger.LogInformation("访问登录页面: {Url}", loginUrl);
            await _page!.GotoAsync(loginUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 查找并点击右上角的二维码图标切换到扫码登录
            _logger.LogInformation("查找二维码切换按钮...");
            var qrSwitchSelectors = new[]
            {
                "[class*='qrcode'], [class*='qr-code'], [title*='二维码'], [aria-label*='二维码']",
                "svg, i, span, button, a" // 可能是图标
            };

            IElementHandle? qrSwitch = null;
            foreach (var selector in qrSwitchSelectors)
            {
                try
                {
                    var elements = await _page.QuerySelectorAllAsync(selector);
                    foreach (var element in elements)
                    {
                        if (await element.IsVisibleAsync())
                        {
                            var html = await element.EvaluateAsync<string>("el => el.outerHTML");
                            if (html.Contains("qr") || html.Contains("二维码"))
                            {
                                qrSwitch = element;
                                _logger.LogInformation("找到二维码切换按钮");
                                break;
                            }
                        }
                    }
                    if (qrSwitch != null) break;
                }
                catch { }
            }

            if (qrSwitch != null)
            {
                _logger.LogInformation("点击二维码切换按钮...");
                await qrSwitch.ClickAsync();
                await Task.Delay(2000);
            }

            // 查找二维码图片
            _logger.LogInformation("查找二维码图片...");
            var qrCodeSelectors = new[]
            {
                "img[alt*='qr'], img[alt*='二维码']",
                "canvas",
                "[class*='qrcode'] img, [class*='qr-code'] img",
                "img[src*='qrcode'], img[src*='qr']"
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
                            _logger.LogInformation("找到二维码元素，选择器: {Selector}", selector);
                            break;
                        }
                    }
                    if (qrCodeElement != null) break;
                }
                catch { }
            }

            if (qrCodeElement == null)
            {
                throw new Exception("未找到二维码元素，建议使用 interactive_login 工具");
            }

            // 获取二维码图片
            string qrCodeBase64;
            var src = await qrCodeElement.GetAttributeAsync("src");
            
            if (!string.IsNullOrEmpty(src) && src.StartsWith("data:image"))
            {
                qrCodeBase64 = src.Split(',')[1];
                _logger.LogInformation("获取到 Base64 二维码");
            }
            else if (!string.IsNullOrEmpty(src))
            {
                _logger.LogInformation("下载二维码图片: {Url}", src);
                qrCodeBase64 = await DownloadImageAsBase64(src);
            }
            else
            {
                _logger.LogInformation("截图方式获取二维码");
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
                Message = $"获取登录二维码失败: {ex.Message}\n建议使用 interactive_login 工具进行首次登录"
            };
        }
    }
}
