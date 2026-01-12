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
            
            // 注意：浏览器模式（headless/headed）在首次初始化时已确定
            // 这里直接初始化即可，模式由构造函数参数控制
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

            // 新版流程：点击 .img 容器触发二维码显示
            var imgContainer = await _page.QuerySelectorAsync("div.img");
            if (imgContainer == null || !await imgContainer.IsVisibleAsync())
            {
                throw new Exception("未找到二维码入口容器（div.img）");
            }

            _logger.LogInformation("点击二维码入口容器（div.img）...");
            // 直接使用 JS 点击，避免被拦截导致 Playwright 自动重试超时
            await imgContainer.EvaluateAsync("el => el.click()");

            // 点击后需要等待二维码 canvas 渲染（增加延迟确保二维码完全加载）
            _logger.LogInformation("等待二维码 canvas 渲染...");
            await Task.Delay(1000);
            
            // 等待并获取二维码 canvas 元素
            await _page.WaitForSelectorAsync("canvas#login-qrcode", new() { Timeout = 15000 });
            var canvas = await _page.QuerySelectorAsync("canvas#login-qrcode") 
                         ?? throw new Exception("未找到二维码 canvas（canvas#login-qrcode）");
            
            _logger.LogInformation("已找到二维码 canvas 元素");
            string? dataUrl = null;
            try
            {
                dataUrl = await canvas.EvaluateAsync<string>("c => c.toDataURL('image/png')");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "canvas.toDataURL 失败，尝试截图方式获取二维码");
            }

            string qrCanvasBase64;
            if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                qrCanvasBase64 = dataUrl.Split(',')[1];
                _logger.LogInformation("已从 canvas.toDataURL 获取到 Base64 二维码");
            }
            else
            {
                var screenshot = await canvas.ScreenshotAsync();
                qrCanvasBase64 = Convert.ToBase64String(screenshot);
                _logger.LogInformation("已通过截图方式获取到 Base64 二维码");
            }

            _logger.LogInformation("二维码获取成功，等待扫码登录...");
            await WaitForLoginAsync();

            return new QrCodeInfo
            {
                QrCodeBase64 = qrCanvasBase64,
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
