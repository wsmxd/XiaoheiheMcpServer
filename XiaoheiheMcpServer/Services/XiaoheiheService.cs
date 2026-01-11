using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 小黑盒服务 - 使用Playwright
/// </summary>
public class XiaoheiheService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly string _cookiesPath;
    private readonly ILogger<XiaoheiheService> _logger;
    private readonly bool _headless;
    private const string BaseUrl = "https://www.xiaoheihe.cn";

    public XiaoheiheService(ILogger<XiaoheiheService> logger, bool headless = true)
    {
        _logger = logger;
        _headless = headless;
        _cookiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "cookies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_cookiesPath)!);
    }

    /// <summary>
    /// 初始化浏览器
    /// </summary>
    private async Task InitializeBrowserAsync()
    {
        if (_browser != null) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-blink-features=AutomationControlled"
            }
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
            var qrCodeElement = await _page.QuerySelectorAsync("img[alt*='qr'], img[alt*='二维码'], canvas, [class*='qrcode'], [class*='qr-code']");
            
            if (qrCodeElement == null)
            {
                throw new Exception("未找到二维码元素");
            }

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

    /// <summary>
    /// 发布图文内容
    /// </summary>
    public async Task<McpToolResult> PublishContentAsync(PublishContentArgs args)
    {
        try
        {
            _logger.LogInformation($"开始发布内容: {args.Title}");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/image_text");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 填写标题
            var titleSelector = "input[placeholder*='标题'], [class*='title'] input";
            await _page.WaitForSelectorAsync(titleSelector);
            await _page.FillAsync(titleSelector, args.Title);
            await Task.Delay(500);

            // 填写内容
            var contentSelector = "textarea, [contenteditable='true'], [class*='content'] textarea";
            var contentText = args.Content;
            
            if (args.Tags.Any())
            {
                contentText += "\n" + string.Join(" ", args.Tags.Select(t => $"#{t}"));
            }
            
            await _page.FillAsync(contentSelector, contentText);
            await Task.Delay(500);

            // 上传图片
            if (args.Images.Any())
            {
                var fileInput = await _page.QuerySelectorAsync("input[type='file']");
                if (fileInput != null)
                {
                    var validImages = args.Images.Where(File.Exists).ToArray();
                    if (validImages.Any())
                    {
                        await fileInput.SetInputFilesAsync(validImages);
                        await Task.Delay(2000 * validImages.Length);
                    }
                }
            }

            // 点击发布按钮
            var publishSelector = "button[class*='publish'], button:has-text('发布')";
            await _page.WaitForSelectorAsync(publishSelector);
            await _page.ClickAsync(publishSelector);
            await Task.Delay(3000);

            await SaveCookiesAsync();

            _logger.LogInformation("内容发布成功");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"✅ 发布成功！\n标题: {args.Title}\n内容已发布到小黑盒" }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布内容失败");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"❌ 发布失败: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    /// <summary>
    /// 发布评论
    /// </summary>
    public async Task<McpToolResult> PostCommentAsync(CommentArgs args)
    {
        try
        {
            _logger.LogInformation($"发布评论到帖子: {args.PostId}");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/post/{args.PostId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            var commentSelector = "textarea[placeholder*='评论'], input[placeholder*='评论'], [class*='comment'] textarea";
            await _page.WaitForSelectorAsync(commentSelector);
            await _page.FillAsync(commentSelector, args.Content);
            await Task.Delay(500);

            var submitSelector = "button[class*='submit'], button:has-text('发送'), button:has-text('评论')";
            await _page.ClickAsync(submitSelector);
            await Task.Delay(2000);

            await SaveCookiesAsync();

            _logger.LogInformation("评论发布成功");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"✅ 评论发布成功！\n内容: {args.Content}" }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布评论失败");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"❌ 发布评论失败: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    /// <summary>
    /// 搜索内容
    /// </summary>
    public async Task<McpToolResult> SearchAsync(SearchArgs args)
    {
        try
        {
            _logger.LogInformation($"搜索关键词: {args.Keyword}");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/search?keyword={Uri.EscapeDataString(args.Keyword)}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            var posts = await _page.QuerySelectorAllAsync("[class*='post-item'], [class*='article'], [class*='search-item']");
            var results = new List<string>();

            foreach (var post in posts.Take(args.PageSize))
            {
                try
                {
                    var title = await post.QuerySelectorAsync("[class*='title'], h3, h2");
                    var author = await post.QuerySelectorAsync("[class*='author'], [class*='user']");
                    var link = await post.QuerySelectorAsync("a");

                    if (title != null)
                    {
                        var titleText = await title.TextContentAsync();
                        var authorText = author != null ? await author.TextContentAsync() : "未知作者";
                        var linkHref = link != null ? await link.GetAttributeAsync("href") : "";

                        results.Add($"• {titleText?.Trim()}\n  作者: {authorText?.Trim()}\n  链接: {linkHref}");
                    }
                }
                catch { continue; }
            }

            var resultText = results.Any()
                ? $"找到 {results.Count} 条结果:\n\n{string.Join("\n\n", results)}"
                : "未找到相关内容";

            return new McpToolResult
            {
                Content = new List<McpContent> { new() { Type = "text", Text = resultText } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索失败");
            return new McpToolResult
            {
                Content = new List<McpContent> { new() { Type = "text", Text = $"❌ 搜索失败: {ex.Message}" } },
                IsError = true
            };
        }
    }

    /// <summary>
    /// 获取帖子详情
    /// </summary>
    public async Task<McpToolResult> GetPostDetailAsync(PostDetailArgs args)
    {
        try
        {
            _logger.LogInformation($"获取帖子详情: {args.PostId}");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/post/{args.PostId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            var title = await _page.TextContentAsync("[class*='title'], h1") ?? "无标题";
            var content = await _page.TextContentAsync("[class*='content'], [class*='article']") ?? "无内容";
            var author = await _page.TextContentAsync("[class*='author']") ?? "未知作者";
            var likes = await _page.TextContentAsync("[class*='like']") ?? "0";
            var comments = await _page.TextContentAsync("[class*='comment-count']") ?? "0";

            var detailText = $"标题: {title.Trim()}\n" +
                           $"作者: {author.Trim()}\n" +
                           $"点赞: {likes.Trim()}\n" +
                           $"评论: {comments.Trim()}\n\n" +
                           $"内容:\n{content.Trim()}";

            return new McpToolResult
            {
                Content = new List<McpContent> { new() { Type = "text", Text = detailText } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取帖子详情失败");
            return new McpToolResult
            {
                Content = new List<McpContent> { new() { Type = "text", Text = $"❌ 获取帖子详情失败: {ex.Message}" } },
                IsError = true
            };
        }
    }

    #region 私有方法

    private async Task WaitForLoginAsync()
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

    private async Task SaveCookiesAsync()
    {
        try
        {
            if (_context == null) return;

            var cookies = await _context.CookiesAsync();
            var cookiesJson = Newtonsoft.Json.JsonConvert.SerializeObject(cookies);
            await File.WriteAllTextAsync(_cookiesPath, cookiesJson);
            _logger.LogInformation("Cookies已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存Cookies失败");
        }
    }

    private async Task LoadCookiesAsync()
    {
        try
        {
            if (!File.Exists(_cookiesPath) || _context == null) return;

            var cookiesJson = await File.ReadAllTextAsync(_cookiesPath);
            var cookies = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Microsoft.Playwright.Cookie>>(cookiesJson);

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

    private async Task<string> DownloadImageAsBase64(string imageUrl)
    {
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
        return Convert.ToBase64String(imageBytes);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
