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

            await _page!.GotoAsync($"{BaseUrl}/app/bbs/link/{args.PostId}");
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

            await _page!.GotoAsync($"{BaseUrl}/app/search?q={Uri.EscapeDataString(args.Keyword)}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 查找所有搜索结果项：.search-result__link（注意是双下划线）
            var resultItems = await _page.QuerySelectorAllAsync(".search-result__link");
            var results = new List<SearchResultItem>();

            foreach (var item in resultItems.Take(Math.Min(args.PageSize, 20)))
            {
                try
                {
                    // 获取帖子链接和ID
                    var linkElement = await item.QuerySelectorAsync("a[href*='/app/bbs/link/']");
                    var href = linkElement != null ? await linkElement.GetAttributeAsync("href") : "";
                    var postId = ExtractPostId(href ?? "");

                    if (string.IsNullOrEmpty(postId)) continue;

                    // 获取标题：div.bbs-content__title 内的文本（包含emoji）
                    var titleElement = await item.QuerySelectorAsync("div.bbs-content__title");
                    var title = titleElement != null ? await titleElement.TextContentAsync() : "无标题";

                    // 获取评论数：span.content-list__comment-cnt
                    var commentElement = await item.QuerySelectorAsync("span.content-list__comment-cnt");
                    var commentText = commentElement != null ? await commentElement.TextContentAsync() : "0";
                    int.TryParse(commentText?.Trim() ?? "0", out var commentCount);

                    // 获取点赞数：span.content-list__like-cnt
                    var likeElement = await item.QuerySelectorAsync("span.content-list__like-cnt");
                    var likeText = likeElement != null ? await likeElement.TextContentAsync() : "0";
                    int.TryParse(likeText?.Trim() ?? "0", out var likeCount);

                    // 获取图片：div.hb-opt__image.pointer.bb-content__image
                    var imageElements = await item.QuerySelectorAllAsync("div.hb-opt__image.pointer.bb-content__image");
                    var imageUrls = new List<string>();
                    foreach (var imgElement in imageElements)
                    {
                        var style = await imgElement.GetAttributeAsync("style") ?? "";
                        // 从style中提取backgroundImage URL（如果有）
                        var bgMatch = System.Text.RegularExpressions.Regex.Match(style, @"background-image:\s*url\(['""]*(.+?)['""]*\)");
                        if (bgMatch.Success)
                            imageUrls.Add(bgMatch.Groups[1].Value);
                    }

                    results.Add(new SearchResultItem
                    {
                        PostId = postId,
                        Title = (title ?? "").Trim(),
                        Link = href,
                        CommentCount = commentCount,
                        LikeCount = likeCount,
                        ImageUrls = imageUrls
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "提取搜索结果项失败");
                    continue;
                }
            }

            await SaveCookiesAsync();

            var resultText = results.Count != 0
                ? $"找到 {results.Count} 条结果：\n\n" + 
                  string.Join("\n\n", results.Select(r => 
                    $"📌 **{r.Title}**\n" +
                    $"📝 评论: {r.CommentCount} | 👍 点赞: {r.LikeCount}\n" +
                    (r.ImageUrls.Count > 0 ? $"🖼️ 图片: {r.ImageUrls.Count} 张\n" : "") +
                    $"🔗 {r.Link}"))
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
    /// 从URL中提取帖子ID
    /// </summary>
    private static string ExtractPostId(string url)
    {
        // 格式: /app/bbs/link/{postId}?...
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/app/bbs/link/(\d+)");
        return match.Success ? match.Groups[1].Value : "";
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

            await _page!.GotoAsync($"{BaseUrl}/app/bbs/link/{args.PostId}");
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
