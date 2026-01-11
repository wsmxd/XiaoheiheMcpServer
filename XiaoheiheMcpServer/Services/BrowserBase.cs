using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 浏览器基础服务 - 处理Playwright生命周期和Cookies管理（支持浏览器实例复用）
/// </summary>
public abstract class BrowserBase : IAsyncDisposable
{
    // 静态共享资源 - 所有实例共享同一个浏览器
    private static IPlaywright? _sharedPlaywright;
    private static IBrowser? _sharedBrowser;
    private static IBrowserContext? _sharedContext;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);
    private static bool _headlessMode = true;
    
    // 实例专用资源 - 每个服务实例有自己的标签页
    protected IPage? _page;
    protected readonly string _cookiesPath;
    protected readonly ILogger _logger;
    protected const string BaseUrl = "https://www.xiaoheihe.cn";

    protected BrowserBase(ILogger logger, bool headless = true)
    {
        _logger = logger;
        _headlessMode = headless;
        _cookiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "cookies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_cookiesPath)!);
    }

    /// <summary>
    /// 初始化浏览器（复用共享浏览器实例，创建新标签页）
    /// </summary>
    protected async Task InitializeBrowserAsync()
    {
        // 如果当前实例已有页面，直接返回
        if (_page != null) return;

        await _browserLock.WaitAsync();
        try
        {
            // 初始化共享浏览器实例（只在第一次调用时创建）
            if (_sharedBrowser == null)
            {
                _logger.LogInformation("首次启动，初始化共享浏览器实例...");
                _sharedPlaywright = await Playwright.CreateAsync();
                _sharedBrowser = await _sharedPlaywright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = _headlessMode,
                    Args =
                    [
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-blink-features=AutomationControlled"
                    ]
                });

                _sharedContext = await _sharedBrowser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                });

                await LoadCookiesAsync();
                _logger.LogInformation("共享浏览器实例初始化完成");
            }

            // 为当前服务实例创建新标签页
            _page = await _sharedContext!.NewPageAsync();
            _logger.LogInformation("创建新标签页，当前浏览器共有 {Count} 个标签页", _sharedContext.Pages.Count);
        }
        finally
        {
            _browserLock.Release();
        }
    }

    /// <summary>
    /// 保存Cookies到文件
    /// </summary>
    protected async Task SaveCookiesAsync()
    {
        try
        {
            if (_sharedContext == null) return;

            var cookies = await _sharedContext.CookiesAsync();
            var cookiesJson = JsonConvert.SerializeObject(cookies);
            await File.WriteAllTextAsync(_cookiesPath, cookiesJson);
            _logger.LogInformation("Cookies已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存Cookies失败");
        }
    }

    /// <summary>
    /// 从文件加载Cookies
    /// </summary>
    protected async Task LoadCookiesAsync()
    {
        try
        {
            if (!File.Exists(_cookiesPath) || _sharedContext == null) return;

            var cookiesJson = await File.ReadAllTextAsync(_cookiesPath);
            var cookies = JsonConvert.DeserializeObject<List<Microsoft.Playwright.Cookie>>(cookiesJson);

            if (cookies != null && cookies.Any())
            {
                await _sharedContext.AddCookiesAsync(cookies);
                _logger.LogInformation("Cookies已加载");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载Cookies失败");
        }
    }

    /// <summary>
    /// 等待登录完成
    /// </summary>
    protected async Task WaitForLoginAsync()
    {
        try
        {
            await _page!.WaitForSelectorAsync("[class*='user'], [class*='avatar'], [class*='login-user']",
                new PageWaitForSelectorOptions { Timeout = 300000 });

            await SaveCookiesAsync();
            _logger.LogInformation("登录成功，已保存Cookies");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("登录超时");
        }
    }

    /// <summary>
    /// 下载图片为Base64字符串
    /// </summary>
    protected async Task<string> DownloadImageAsBase64(string imageUrl)
    {
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
        return Convert.ToBase64String(imageBytes);
    }

    public virtual async ValueTask DisposeAsync()
    {
        // 只关闭当前标签页，不关闭共享的浏览器实例
        if (_page != null)
        {
            await _page.CloseAsync();
            _logger.LogInformation("标签页已关闭，剩余 {Count} 个标签页", _sharedContext?.Pages.Count ?? 0);
        }
        
        // 注意：不释放 _sharedBrowser、_sharedContext 和 _sharedPlaywright
        // 它们会在应用程序退出时自动清理，或者可以添加专门的静态清理方法
    }

    /// <summary>
    /// 静态方法：清理共享浏览器资源（应用程序退出时调用）
    /// </summary>
    public static async Task CleanupSharedBrowserAsync()
    {
        await _browserLock.WaitAsync();
        try
        {
            if (_sharedContext != null)
            {
                await _sharedContext.CloseAsync();
                _sharedContext = null;
            }

            if (_sharedBrowser != null)
            {
                await _sharedBrowser.CloseAsync();
                _sharedBrowser = null;
            }

            _sharedPlaywright?.Dispose();
            _sharedPlaywright = null;
        }
        finally
        {
            _browserLock.Release();
        }
    }
}
