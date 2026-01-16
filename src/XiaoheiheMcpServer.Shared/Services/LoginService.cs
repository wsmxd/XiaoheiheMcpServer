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
            await Task.Delay(400);

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
                
                // 获取用户信息
                await Task.Delay(800); // 等待页面稳定
                var userElement = await _page.QuerySelectorAsync("p.user-box__username");
                var username = userElement is not null ? await userElement.TextContentAsync() : null;

                return new LoginStatus
                {
                    IsLoggedIn = true,
                    Username = username?.Trim() ?? "xiaoheihe-user",
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
            await Task.Delay(2000); // 增加延迟到2秒，确保二维码完全渲染
            
            // 等待并获取二维码 canvas 元素
            await _page.WaitForSelectorAsync("canvas#login-qrcode", new() { Timeout = 15000 });
            var canvas = await _page.QuerySelectorAsync("canvas#login-qrcode") 
                         ?? throw new Exception("未找到二维码 canvas（canvas#login-qrcode）");
            
            _logger.LogInformation("已找到二维码 canvas 元素，准备获取二维码数据");
            
            // 直接调用 toDataURL() 获取二维码 Base64 数据（与浏览器控制台测试一致）
            var dataUrl = await canvas.EvaluateAsync<string>("c => c.toDataURL()");
            
            if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("canvas.toDataURL() 返回的数据格式不正确");
            }

            _logger.LogInformation("二维码获取成功，返回给用户扫描");

            // 创建用于管理监听任务的 CancellationTokenSource
            _loginMonitorCts = new CancellationTokenSource();
            
            // 启动后台任务监听登录状态，登录成功后自动保存 Cookie 并取消任务
            _ = Task.Run(async () => await MonitorAndSaveLoginAsync(_loginMonitorCts.Token));

            return new QrCodeInfo
            {
                DataUrl = dataUrl,
                ExpireTime = DateTime.Now.AddMinutes(2),
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
}
