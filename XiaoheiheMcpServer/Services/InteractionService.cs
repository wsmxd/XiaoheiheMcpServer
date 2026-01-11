using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 小黑盒互动服务 - 处理评论、搜索、帖子详情等
/// </summary>
public class InteractionService : BrowserBase
{
    public InteractionService(ILogger<InteractionService> logger, bool headless = true)
        : base(logger, headless)
    {
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
                Content =
                [
                    new() { Type = "text", Text = $"❌ 发布评论失败: {ex.Message}" }
                ],
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

            var resultText = results.Count != 0
                ? $"找到 {results.Count} 条结果:\n\n{string.Join("\n\n", results)}"
                : "未找到相关内容";

            return new McpToolResult
            {
                Content = [new() { Type = "text", Text = resultText }]
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
                Content = [new() { Type = "text", Text = detailText }]
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
}
