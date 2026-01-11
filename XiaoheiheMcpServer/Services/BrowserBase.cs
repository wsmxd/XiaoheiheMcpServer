using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 浏览器基础服务 - 处理Playwright生命周期和Cookies管理
/// </summary>
public abstract class BrowserBase : IAsyncDisposable
{
    protected IPlaywright? _playwright;
    protected IBrowser? _browser;
    protected IBrowserContext? _context;
    protected IPage? _page;
    protected readonly string _cookiesPath;
    protected readonly ILogger _logger;
    protected readonly bool _headless;
    protected const string BaseUrl = "https://www.xiaoheihe.cn";

    protected BrowserBase(ILogger logger, bool headless = true)
    {
        _logger = logger;
        _headless = headless;
        _cookiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "cookies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_cookiesPath)!);
    }

    /// <summary>
    /// 初始化浏览器
    /// </summary>
    protected async Task InitializeBrowserAsync()
    {
        if (_browser != null) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            Args =
            [
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-blink-features=AutomationControlled"
            ]
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        });

        await LoadCookiesAsync();
        _page = await _context.NewPageAsync();
    }

    /// <summary>
    /// 保存Cookies到文件
    /// </summary>
    protected async Task SaveCookiesAsync()
    {
        try
        {
            if (_context == null) return;

            var cookies = await _context.CookiesAsync();
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
            if (!File.Exists(_cookiesPath) || _context == null) return;

            var cookiesJson = await File.ReadAllTextAsync(_cookiesPath);
            var cookies = JsonConvert.DeserializeObject<List<Microsoft.Playwright.Cookie>>(cookiesJson);

            if (cookies != null && cookies.Any())
            {
                await _context.AddCookiesAsync(cookies);
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
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
