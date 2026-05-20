using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace XiaoheiheMcpServer.Shared.Services;

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
    private const int MinimumChromiumMajorVersion = 120;
    
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

                var launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = _headlessMode,
                    Args =
                    [
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-blink-features=AutomationControlled"
                    ]
                };

                var chromiumExecutablePath = FindChromiumExecutable(_logger);
                if (!string.IsNullOrWhiteSpace(chromiumExecutablePath))
                {
                    launchOptions.ExecutablePath = chromiumExecutablePath;
                    _logger.LogInformation("使用本地 Chromium/Chrome 可执行文件: {Path}", chromiumExecutablePath);
                }
                else
                {
                    _logger.LogWarning("未找到主版本 >= {Version} 的本地 Chromium/Chrome，将回退到 Playwright 默认浏览器。", MinimumChromiumMajorVersion);
                }

                _sharedBrowser = await _sharedPlaywright.Chromium.LaunchAsync(launchOptions);

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

    private static string? FindChromiumExecutable(ILogger logger)
    {
        foreach (var candidate in GetChromiumExecutableCandidates().Distinct(GetPathComparer()))
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            var majorVersion = GetChromiumMajorVersion(candidate);
            if (majorVersion >= MinimumChromiumMajorVersion)
            {
                return candidate;
            }

            if (majorVersion.HasValue)
            {
                logger.LogWarning(
                    "忽略 Chromium/Chrome 可执行文件 {Path}，主版本 {Version} 低于最低要求 {MinimumVersion}",
                    candidate,
                    majorVersion.Value,
                    MinimumChromiumMajorVersion);
            }
            else
            {
                logger.LogDebug("无法识别 Chromium/Chrome 版本，跳过: {Path}", candidate);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetChromiumExecutableCandidates()
    {
        var explicitPath = Environment.GetEnvironmentVariable("XIAOHEIHE_CHROMIUM_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath;
        }

        foreach (var root in GetPlaywrightBrowserRoots())
        {
            foreach (var executable in GetPlaywrightChromiumExecutables(root))
            {
                yield return executable;
            }
        }

        foreach (var executable in GetCommonChromiumExecutables())
        {
            yield return executable;
        }

        foreach (var executable in GetPathChromiumExecutables())
        {
            yield return executable;
        }
    }

    private static IEnumerable<string> GetPlaywrightBrowserRoots()
    {
        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrWhiteSpace(browsersPath) && browsersPath != "0")
        {
            yield return browsersPath;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "ms-playwright");

        var defaultPath = GetDefaultPlaywrightBrowsersPath();
        if (!string.IsNullOrWhiteSpace(defaultPath))
        {
            yield return defaultPath;
        }
    }

    private static string? GetDefaultPlaywrightBrowsersPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(localAppData) ? null : Path.Combine(localAppData, "ms-playwright");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return null;
        }

        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Caches", "ms-playwright")
            : Path.Combine(home, ".cache", "ms-playwright");
    }

    private static IEnumerable<string> GetPlaywrightChromiumExecutables(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        foreach (var directory in Directory.GetDirectories(root, "chromium-*")
                     .OrderByDescending(ExtractTrailingNumber))
        {
            if (OperatingSystem.IsWindows())
            {
                yield return Path.Combine(directory, "chrome-win", "chrome.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return Path.Combine(directory, "chrome-mac", "Chromium.app", "Contents", "MacOS", "Chromium");
            }
            else
            {
                yield return Path.Combine(directory, "chrome-linux", "chrome");
            }
        }
    }

    private static IEnumerable<string> GetCommonChromiumExecutables()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            yield return Path.Combine(localAppData, "Chromium", "Application", "chrome.exe");
            yield return Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe");
            yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
            yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
            yield return Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
            yield return Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            yield return "/Applications/Chromium.app/Contents/MacOS/Chromium";
            yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";

            if (!string.IsNullOrWhiteSpace(home))
            {
                yield return Path.Combine(home, "Applications", "Chromium.app", "Contents", "MacOS", "Chromium");
                yield return Path.Combine(home, "Applications", "Google Chrome.app", "Contents", "MacOS", "Google Chrome");
                yield return Path.Combine(home, "Applications", "Microsoft Edge.app", "Contents", "MacOS", "Microsoft Edge");
            }

            yield break;
        }

        yield return "/usr/bin/chromium";
        yield return "/usr/bin/chromium-browser";
        yield return "/usr/bin/google-chrome";
        yield return "/usr/bin/google-chrome-stable";
        yield return "/usr/bin/microsoft-edge";
        yield return "/usr/bin/microsoft-edge-stable";
        yield return "/snap/bin/chromium";
    }

    private static IEnumerable<string> GetPathChromiumExecutables()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var executableNames = OperatingSystem.IsWindows()
            ? ["chrome.exe", "chromium.exe", "msedge.exe"]
            : new[] { "chromium", "chromium-browser", "google-chrome", "google-chrome-stable", "microsoft-edge", "microsoft-edge-stable" };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedDirectory = directory.Trim('"');
            foreach (var executableName in executableNames)
            {
                yield return Path.Combine(trimmedDirectory, executableName);
            }
        }
    }

    private static int? GetChromiumMajorVersion(string executablePath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executablePath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit(3000))
            {
                process.Kill(true);
                return null;
            }

            var versionText = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            var match = Regex.Match(versionText, @"\b(\d{2,3})\.\d+\.");
            return match.Success ? int.Parse(match.Groups[1].Value) : null;
        }
        catch
        {
            return null;
        }
    }

    private static long ExtractTrailingNumber(string path)
    {
        var match = Regex.Match(Path.GetFileName(path), @"(\d+)$");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
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
