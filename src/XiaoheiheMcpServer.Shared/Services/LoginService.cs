using Microsoft.Playwright;
using XiaoheiheMcpServer.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace XiaoheiheMcpServer.Shared.Services;

/// <summary>
/// 小黑盒登录管理服务
/// </summary>
public class LoginService : BrowserBase
{
    private CancellationTokenSource? _loginMonitorCts;

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

            var username = await FindLoggedInUsernameAsync();

            if (!string.IsNullOrWhiteSpace(username))
            {
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
    /// <param name="waitTimeoutSeconds">等待用户登录的超时时间（秒），默认180秒（3分钟）</param>
    public async Task<LoginStatus> InteractiveLoginAsync(int waitTimeoutSeconds = 180)
    {
        try
        {
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
                // 等待跳转完成，最长 3 分钟
                await _page.WaitForURLAsync(new Regex(@".*\.?xiaoheihe\.cn/home$"), 
                    new() { Timeout = waitTimeoutSeconds * 1000 }
                );

                _logger.LogInformation("检测到登录成功！");
                
                // 保存 Cookie
                await SaveCookiesAsync();
                
                // 获取用户信息。新版首页不再使用 p.user-box__username，统一走兼容选择器。
                var username = await FindLoggedInUsernameAsync();

                if (string.IsNullOrWhiteSpace(username))
                {
                    return new LoginStatus
                    {
                        IsLoggedIn = false,
                        Message = "登录页面已跳转，但未确认当前账号，请重新检查登录状态"
                    };
                }

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
    /// 从当前页面的顶部导航读取登录用户。
    /// 小黑盒前端更新后，旧的 p.user-box__username 选择器可能不存在；
    /// 必须以当前页面实际渲染的账号元素为准，避免把已登录状态误判为未登录。
    /// </summary>
    private async Task<string?> FindLoggedInUsernameAsync()
    {
        if (_page is null)
        {
            return null;
        }

        // 只检查全局顶部导航。不要使用 .link-user__username 等文章作者选择器，
        // 否则未登录首页中的其他作者也可能被误判为当前登录账号。
        var selectors = new[]
        {
            "p.user-box__username",       // 旧版首页
            ".nav-user-popover-name"      // 当前顶部用户菜单
        };

        try
        {
            // 顶部导航按页面数据异步渲染，等待实际元素而不是依赖固定延迟。
            await _page.WaitForSelectorAsync(
                "p.user-box__username, .nav-user-trigger, img.nav-user-avatar",
                new() { State = WaitForSelectorState.Attached, Timeout = 3000 });
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("等待顶部用户导航超时，继续检查已有元素");
        }
        catch (PlaywrightException ex)
        {
            _logger.LogDebug(ex, "等待顶部用户导航失败，继续检查已有元素");
        }

        foreach (var selector in selectors)
        {
            IElementHandle? element;
            try
            {
                element = await _page.QuerySelectorAsync(selector);
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "读取登录导航元素失败: {Selector}", selector);
                continue;
            }

            var text = await ReadElementTextAsync(element, selector);

            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "登录", StringComparison.Ordinal))
            {
                return text;
            }
        }

        // 顶部头像即使在用户菜单关闭时也会存在，可作为当前登录账号的兜底标识。
        IElementHandle? avatar;
        try
        {
            avatar = await _page.QuerySelectorAsync("img.nav-user-avatar");
        }
        catch (PlaywrightException ex)
        {
            _logger.LogDebug(ex, "读取顶部用户头像失败");
            avatar = null;
        }
        var avatarAlt = await ReadElementAttributeAsync(avatar, "alt", "img.nav-user-avatar");
        if (!string.IsNullOrWhiteSpace(avatarAlt))
        {
            var avatarName = Regex.Replace(avatarAlt, @"头像$", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(avatarName) && !string.Equals(avatarName, "登录", StringComparison.Ordinal))
            {
                return avatarName;
            }
        }

        // 菜单可能按需渲染；点击顶部用户入口后再检查一次。
        IElementHandle? trigger;
        try
        {
            trigger = await _page.QuerySelectorAsync(".nav-user-trigger");
        }
        catch (PlaywrightException ex)
        {
            _logger.LogDebug(ex, "读取顶部用户入口失败");
            trigger = null;
        }
        if (trigger is not null)
        {
            try
            {
                await trigger.ClickAsync(new() { Timeout = 2000 });
                await Task.Delay(150);
                var popover = await _page.QuerySelectorAsync(".nav-user-popover-name");
                var text = popover is null ? null : (await popover.TextContentAsync())?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "登录", StringComparison.Ordinal))
                {
                    return text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "打开顶部用户菜单读取登录用户名失败");
            }
        }

        return null;
    }

    private async Task<string?> ReadElementTextAsync(IElementHandle? element, string selector)
    {
        if (element is null)
        {
            return null;
        }

        try
        {
            return (await element.TextContentAsync())?.Trim();
        }
        catch (PlaywrightException ex)
        {
            _logger.LogDebug(ex, "读取登录导航文本失败: {Selector}", selector);
            return null;
        }
    }

    private async Task<string?> ReadElementAttributeAsync(IElementHandle? element, string attribute, string selector)
    {
        if (element is null)
        {
            return null;
        }

        try
        {
            return (await element.GetAttributeAsync(attribute))?.Trim();
        }
        catch (PlaywrightException ex)
        {
            _logger.LogDebug(ex, "读取登录导航属性失败: {Selector}.{Attribute}", selector, attribute);
            return null;
        }
    }

    /// <summary>
    /// 获取登录二维码
    /// </summary>
    public async Task<QrCodeInfo> GetLoginQrCodeAsync()
    {
        try
        {
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
            await Task.Delay(2000); // 增加延迟到2秒，确保二维码完全渲染
            
            // 等待并获取二维码 canvas 元素
            await _page.WaitForSelectorAsync("canvas#login-qrcode", new() { Timeout = 15000 });
            var canvas = await _page.QuerySelectorAsync("canvas#login-qrcode") 
                         ?? throw new Exception("未找到二维码 canvas（canvas#login-qrcode）");
            
            _logger.LogInformation("已找到二维码 canvas 元素，准备获取二维码数据");
            
            var qrdata = await canvas.ScreenshotAsync(new()
            {
                Type = ScreenshotType.Png,
                OmitBackground = true
            });
            await File.WriteAllBytesAsync("qrcode.png", qrdata);
            var qrPath = Path.GetFullPath("qrcode.png");

            _logger.LogInformation("二维码获取成功，返回给用户扫描");

            // 创建用于管理监听任务的 CancellationTokenSource
            //_loginMonitorCts = new CancellationTokenSource();
            
            // 启动后台任务监听登录状态，登录成功后自动保存 Cookie 并取消任务
            //_ = Task.Run(async () => await MonitorAndSaveLoginAsync(_loginMonitorCts.Token));
            OpenImage(qrPath);
            await _page.WaitForURLAsync(new Regex(@".*\.?xiaoheihe\.cn/home$"), 
                    new() { Timeout = 120 * 1000 }
                );

            _logger.LogInformation("检测到登录成功！");
            await SaveCookiesAsync();

            return new QrCodeInfo
            {
                Message = "登录成功！Cookie 已保存，后续操作将自动使用此登录状态"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取登录二维码失败");
            return new QrCodeInfo
            {
                Message = $"获取登录二维码失败: {ex.Message}\n"
            };
        }
    }

    /// <summary>
    /// 后台监听登录状态，登录成功后自动保存 Cookie 并取消任务
    /// </summary>
    private async Task MonitorAndSaveLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("后台监听登录状态...");
            
            var maxWaitTime = TimeSpan.FromMinutes(2);
            var checkInterval = TimeSpan.FromSeconds(5);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < maxWaitTime)
            {
                // 检查是否被请求取消
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 复用检查登录状态的方法
                    var loginStatus = await CheckLoginStatusAsync();
                    
                    if (loginStatus.IsLoggedIn)
                    {
                        _logger.LogInformation("检测到登录成功！用户: {Username}", loginStatus.Username);
                        await SaveCookiesAsync();
                        _logger.LogInformation("Cookie 已自动保存");
                        
                        // 请求取消任务
                        _loginMonitorCts?.Cancel();
                        _logger.LogInformation("监听任务已取消");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "检查登录状态出错");
                }

                // 每 5 秒检查一次
                await Task.Delay(checkInterval, cancellationToken);
            }

            _logger.LogWarning("监听登录状态超时（2分钟内未检测到登录成功），任务终止");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("登录监听任务已被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台监听登录状态失败");
        }
        finally
        {
            // 清理资源
            _loginMonitorCts?.Dispose();
            _loginMonitorCts = null;
        }
    }

    private static void OpenImage(string imagePath)
    {
        var startInfo = OperatingSystem.IsMacOS()
            ? new System.Diagnostics.ProcessStartInfo("open", imagePath)
            : OperatingSystem.IsWindows()
                ? new System.Diagnostics.ProcessStartInfo(imagePath) { UseShellExecute = true }
                : new System.Diagnostics.ProcessStartInfo("xdg-open", imagePath);

        System.Diagnostics.Process.Start(startInfo);
    }
}
