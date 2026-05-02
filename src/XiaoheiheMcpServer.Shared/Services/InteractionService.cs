using Microsoft.Playwright;
using XiaoheiheMcpServer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Shared.Services;

/// <summary>
/// 小黑盒互动服务 - 处理评论、搜索、帖子详情等
/// </summary>
public partial class InteractionService : BrowserBase
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

            // 1) 聚焦评论输入框 (ProseMirror hb-editor)
            var editor = await _page.QuerySelectorAsync(".ProseMirror.hb-editor");
            if (editor == null)
            {
                throw new Exception("未找到评论输入框 .ProseMirror.hb-editor");
            }

            await editor.ClickAsync();
            await Task.Delay(300);
            await _page.Keyboard.PressAsync("Control+A");
            await Task.Delay(100);
            await _page.Keyboard.PressAsync("Delete");
            await Task.Delay(200);
            await _page.Keyboard.TypeAsync(args.Content);
            await Task.Delay(500);

            // 2) 如果有图片，点击图片按钮并上传
            if (args.Images.Any())
            {
                _logger.LogInformation("评论附带图片，开始上传...");
                var validImages = args.Images.Where(File.Exists).ToArray();
                if (validImages.Any())
                {
                    try
                    {
                        var imageBtn = await _page.QuerySelectorAsync("button.link-reply__menu-item.image");
                        if (imageBtn != null)
                        {
                            var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                            {
                                await imageBtn.ClickAsync();
                            });

                            if (fileChooser != null)
                            {
                                await fileChooser.SetFilesAsync(validImages);
                                await Task.Delay(2000 + 1000 * validImages.Length);
                                _logger.LogInformation($"评论图片上传完成: {validImages.Length} 张");
                            }
                            else
                            {
                                _logger.LogWarning("未能捕获评论图片的文件选择器");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("未找到评论图片按钮 .link-reply__menu-item.image");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "评论图片上传失败");
                    }
                }
                else
                {
                    _logger.LogWarning("评论图片路径无有效文件，跳过上传");
                }
            }

            // 3) 点击发布评论按钮
            var submitSelectors = new[]
            {
                ".link-reply__menu-btn.hb-color__btn--confirm",
                "button:has-text('发送')",
                "button:has-text('评论')",
                "button[class*='submit']"
            };

            var clicked = false;
            foreach (var selector in submitSelectors)
            {
                var btn = await _page.QuerySelectorAsync(selector);
                if (btn != null && await btn.IsVisibleAsync())
                {
                    await btn.ClickAsync();
                    clicked = true;
                    break;
                }
            }

            if (!clicked)
            {
                throw new Exception("未找到评论发布按钮");
            }

            await Task.Delay(2000);

            await SaveCookiesAsync();

            _logger.LogInformation("评论发布成功");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"✅ 评论发布成功！\n内容: {args.Content}" }
                ]
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
    /// 回复评论
    /// </summary>
    public async Task<McpToolResult> ReplyCommentAsync(ReplyCommentArgs args)
    {
        try
        {
            _logger.LogInformation("回复评论，帖子: {PostId}, 目标评论: {Target}", args.PostId, 
                args.TargetCommentContent.Length > 30 
                    ? args.TargetCommentContent.Substring(0, 30) + "..." 
                    : args.TargetCommentContent);
            
            await InitializeBrowserAsync();

            // 1) 导航到帖子页面
            await _page!.GotoAsync($"{BaseUrl}/app/bbs/link/{args.PostId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 2) 点击目标评论的回复按钮
            var clicked = await _page.ClickReplyCommentAsync(args.TargetCommentContent, _logger);
            if (!clicked)
            {
                throw new Exception($"未找到内容为 \"{args.TargetCommentContent}\" 的评论，或无法点击回复按钮");
            }
            
            await Task.Delay(500);

            // 3) 聚焦评论输入框 (ProseMirror hb-editor)
            var editor = await _page.QuerySelectorAsync(".ProseMirror.hb-editor");
            if (editor == null)
            {
                throw new Exception("未找到评论输入框 .ProseMirror.hb-editor");
            }

            await editor.ClickAsync();
            await Task.Delay(300);
            await _page.Keyboard.TypeAsync(args.Content);
            await Task.Delay(500);

            // 4) 点击发布评论按钮
            var submitSelectors = new[]
            {
                ".link-reply__menu-btn.hb-color__btn--confirm",
                "button:has-text('发送')",
                "button:has-text('回复')",
                "button[class*='submit']"
            };

            var submitClicked = false;
            foreach (var selector in submitSelectors)
            {
                var btn = await _page.QuerySelectorAsync(selector);
                if (btn != null && await btn.IsVisibleAsync())
                {
                    await btn.ClickAsync();
                    submitClicked = true;
                    break;
                }
            }

            if (!submitClicked)
            {
                throw new Exception("未找到回复发布按钮");
            }

            await Task.Delay(2000);
            await SaveCookiesAsync();

            _logger.LogInformation("评论回复成功");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"✅ 评论回复成功！\n回复内容: {args.Content}" }
                ]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回复评论失败");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"❌ 回复评论失败: {ex.Message}" }
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
                        var bgMatch = MyRegex1().Match(style);
                        if (bgMatch.Success)
                            imageUrls.Add(bgMatch.Groups[1].Value);
                    }

                    results.Add(new SearchResultItem
                    {
                        PostId = postId,
                        Title = (title ?? "").Trim(),
                        Link = postId, // 只保存帖子ID
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
                    $"🔗 帖子ID: {r.Link}"))
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
                Content = [new() { Type = "text", Text = $"❌ 搜索失败: {ex.Message}" }],
                IsError = true
            };
        }
    }

    /// <summary>
    /// 从URL中提取帖子ID
    /// </summary>
    internal static string ExtractPostId(string url)
    {
        // 格式: /app/bbs/link/{postId}?...
        var match = MyRegex().Match(url);
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 获取帖子详情
    /// </summary>
    public async Task<PostDetail> GetPostDetailAsync(PostDetailArgs args)
    {
        try
        {
            _logger.LogInformation($"获取帖子详情: {args.PostId}");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/app/bbs/link/{args.PostId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            var postDetail = new PostDetail { PostId = args.PostId };

            // 1. 获取封面图片：header-image__item-image 下的 img 标签的 src
            var coverElements = await _page.QuerySelectorAllAsync(".header-image__item-image img");
            foreach (var coverElement in coverElements)
            {
                var src = await coverElement.GetAttributeAsync("src");
                if (!string.IsNullOrEmpty(src))
                    postDetail.CoverImages.Add(src);
            }

            // 2. 获取标题：section-title__content 只提取文字
            var titleElement = await _page.QuerySelectorAsync(".section-title__content");
            postDetail.Title = titleElement != null 
                ? (await titleElement.TextContentAsync() ?? "无标题").Trim() 
                : "无标题";

            // 3. 获取正文内容：image-text__content 只提取文字
            var contentElement = await _page.QuerySelectorAsync(".image-text__content");
            postDetail.Content = contentElement != null 
                ? (await contentElement.TextContentAsync() ?? "").Trim() 
                : "";

            // 文章类型：如果正文为空，则从 .post__content 下的多个 p 标签中提取正文
            if (string.IsNullOrWhiteSpace(postDetail.Content))
            {
                try
                {
                    var paragraphElements = await _page.QuerySelectorAllAsync(".post__content p");
                    if (paragraphElements.Count > 0)
                    {
                        var paragraphs = new List<string>(paragraphElements.Count);
                        foreach (var p in paragraphElements)
                        {
                            var text = (await p.TextContentAsync() ?? "").Trim();
                            if (!string.IsNullOrEmpty(text))
                                paragraphs.Add(text);
                        }

                        if (paragraphs.Count > 0)
                        {
                            postDetail.Content = string.Join("\n", paragraphs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "文章正文回退解析失败（.post__content p）");
                }
            }

            // 4. 获取标签：content-tag-text 类（可能有多个）
            var tagElements = await _page.QuerySelectorAllAsync(".content-tag-text");
            foreach (var tagElement in tagElements)
            {
                var tagText = await tagElement.TextContentAsync();
                if (!string.IsNullOrEmpty(tagText))
                    postDetail.Tags.Add(tagText.Trim());
            }

            // 5. 获取评论总数：slide-tab__tab-cnt
            var commentCountElement = await _page.QuerySelectorAllAsync(".link-reply__operation-desc");
            if (commentCountElement.Count > 0)
            {
                var lastElement = commentCountElement.LastOrDefault();
                if (lastElement != null)
                {
                    var countText = await lastElement.TextContentAsync();
                    int.TryParse(countText?.Trim() ?? "0", out var count);
                    postDetail.CommentCount = count;
                }
            }

            // 6. 获取具体评论：每个评论在 link-comment__comment-item 类下
            var commentItems = await _page.QuerySelectorAllAsync(".link-comment__comment-item");
            foreach (var commentItem in commentItems.Take(30)) // 限制最多30条评论
            {
                try
                {
                    var author = await commentItem.QuerySelectorAsync(".info-box__username");
                    var authorName = author != null 
                        ? (await author.TextContentAsync() ?? "").Trim() 
                        : "匿名用户";
                    // 评论内容：comment-item__content 只提取文字
                    var contentElem = await commentItem.QuerySelectorAsync(".comment-item__content");
                    var content = contentElem != null 
                        ? (await contentElem.TextContentAsync() ?? "").Trim() 
                        : "";

                    // 点赞数：like-box__cnt
                    var likeElem = await commentItem.QuerySelectorAsync(".like-box__cnt");
                    var likeText = likeElem != null 
                        ? await likeElem.TextContentAsync() 
                        : "0";
                    int.TryParse(likeText?.Trim() ?? "0", out var likeCount);

                    // 获取回复的评论来构建层级结构（如果有）子评论的作者a.children-item__comment-creator
                    // 回复的评论内容p.children-item__comment-content
                    var replyAuthorLocator = await commentItem.QuerySelectorAsync("a.children-item__comment-creator");
                    string? rAuthor = null;
                    if (replyAuthorLocator != null)
                        rAuthor = (await replyAuthorLocator.TextContentAsync())?.Trim();
                    var replyContentLocator = await commentItem.QuerySelectorAsync("p.children-item__comment-content");
                    string? rContent = null;
                    if (replyContentLocator != null)
                        rContent = (await replyContentLocator.TextContentAsync())?.Trim();

                    if (!string.IsNullOrEmpty(content))
                    {
                        postDetail.Comments.Add(new CommentItem
                        {
                            Author = authorName,
                            Content = content,
                            LikeCount = likeCount,
                            Replies = !string.IsNullOrEmpty(rContent) 
                                ? [new CommentItem { Author = rAuthor ?? "匿名用户", Content = rContent.Trim() }] 
                                : []
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "提取评论项失败");
                    continue;
                }
            }
            await SaveCookiesAsync();

            return postDetail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取帖子详情失败");
            return new PostDetail
            {
                PostId = args.PostId,
                Title = "获取帖子详情失败",
                Content = $"❌ 获取帖子详情失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取首页推荐帖子列表
    /// </summary>
    /// <returns>帖子列表</returns>
    public async Task<List<SearchResultItem>> GetHomePostsAsync()
    {
        try
        {
            _logger.LogInformation("获取首页推荐帖子列表");
            await InitializeBrowserAsync();

            await _page!.GotoAsync($"{BaseUrl}/app/bbs/home");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(400);
            var linkItems = _page.Locator("a[href*='/app/bbs/link/']");
            var linkCount = await linkItems.CountAsync();
            _logger.LogInformation("首页推荐帖子数量: {Count}", linkCount);
            var results = new List<SearchResultItem>();
            for (var i = 0; i < linkCount; i++)
            {
                var linkItem = linkItems.Nth(i);
                var href = await linkItem.GetAttributeAsync("href") ?? "";
                var postId = ExtractPostId(href ?? "");

                if (string.IsNullOrEmpty(postId)) continue;

                var item = linkItem.Locator("xpath=ancestor::*[contains(@class,'content-list__item')][1]");
                var hasItem = await item.CountAsync() > 0;
                var scope = hasItem ? item : linkItem;

                // 获取标题：div.bbs-content__title 内的文本（包含emoji）
                var titleLocator = scope.Locator("div.bbs-content__title");
                var title = await titleLocator.CountAsync() > 0
                    ? await titleLocator.First.TextContentAsync()
                    : "无标题";

                // 获取正文：div.bbs-content__content 内的文本（包含emoji）
                var contentLocator = scope.Locator("div.bbs-content__content");
                var content = await contentLocator.CountAsync() > 0
                    ? await contentLocator.First.TextContentAsync()
                    : "无内容";

                // 获取评论数：span.content-list__comment-cnt
                var commentLocator = scope.Locator("span.content-list__comment-cnt");
                var commentText = await commentLocator.CountAsync() > 0
                    ? await commentLocator.First.TextContentAsync()
                    : "0";
                int.TryParse(commentText?.Trim() ?? "0", out var commentCount);

                // 获取点赞数：span.content-list__like-cnt
                var likeLocator = scope.Locator("span.content-list__like-cnt");
                var likeText = await likeLocator.CountAsync() > 0
                    ? await likeLocator.First.TextContentAsync()
                    : "0";
                int.TryParse(likeText?.Trim() ?? "0", out var likeCount);

                results.Add(new SearchResultItem
                {
                    PostId = postId,
                    Title = (title ?? "").Trim(),
                    ContentPreview = (content ?? "").Trim(),
                    Link = postId, // 只保存帖子ID
                    CommentCount = commentCount,
                    LikeCount = likeCount
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取首页推荐帖子列表失败");
            return [];
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"/app/bbs/link/(\d+)")]
    internal static partial System.Text.RegularExpressions.Regex MyRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"background-image:\s*url\(['""]*(.+?)['""]*\)")]
    internal static partial System.Text.RegularExpressions.Regex MyRegex1();
    [System.Text.RegularExpressions.GeneratedRegex(@"(https?://[^""'\s]+?\.(?:png|jpeg))")]
    internal static partial System.Text.RegularExpressions.Regex MyRegex2();
}
