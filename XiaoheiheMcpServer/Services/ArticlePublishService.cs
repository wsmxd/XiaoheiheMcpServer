using System.Text.RegularExpressions;
using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 专用于“文章(article)”类型发布的服务
/// - 无封面图
/// - 正文工具栏存在 .editor-menu-image__btn 用于插入图片
/// - 支持在正文文本中解析本地绝对图片路径并转化为图片上传，而非输入到编辑器
/// - 社区/话题选择与图文(image_text)一致
/// </summary>
public class ArticlePublishService : BrowserBase
{
    public ArticlePublishService(ILogger<ArticlePublishService> logger, bool headless = true)
        : base(logger, headless)
    {
    }

    public async Task<McpToolResult> PublishArticleAsync(PublishArticleArgs args)
    {
        try
        {
            const int MaxCommunities = 2;
            const int MaxTags = 5;

            if (args.Communities.Count > MaxCommunities)
            {
                return new McpToolResult
                {
                    IsError = true,
                    Content =
                    [
                        new() { Type = "text", Text = $"❌ communities 最多只能传 {MaxCommunities} 个（当前: {args.Communities.Count}）" }
                    ]
                };
            }

            if (args.Tags.Count > MaxTags)
            {
                return new McpToolResult
                {
                    IsError = true,
                    Content =
                    [
                        new() { Type = "text", Text = $"❌ tags 最多只能传 {MaxTags} 个（当前: {args.Tags.Count}）" }
                    ]
                };
            }

            _logger.LogInformation($"[Article] 开始发布文章: {args.Title}");
            await InitializeBrowserAsync();

            // 打开文章编辑器
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/article");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 解析正文：按图片路径分割成文本和图片分段
            var (contentSegments, allImages) = ParseContentWithImages(args.Content);
            
            // 追加args.Images中的额外图片
            if (args.Images != null)
            {
                foreach (var p in args.Images) 
                    if (File.Exists(p) && !allImages.Contains(p, StringComparer.OrdinalIgnoreCase))
                        allImages.Add(p);
            }

            // 1) 标题：使用第一个 ProseMirror 作为标题
            try
            {
                var prose = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (prose.Count > 0)
                {
                    var titleEl = prose[0];
                    await titleEl.ClickAsync();
                    await Task.Delay(300);
                    await _page.Keyboard.TypeAsync(args.Title);
                    await Task.Delay(400);
                    _logger.LogInformation("[Article] 标题已填写");
                }
                else
                {
                    _logger.LogWarning("[Article] 未找到标题 ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Article] 填写标题失败");
            }

            // 2) 正文与图片交替填充：先文本 -> 插图片 -> 文本 -> 插图片...
            IElementHandle? contentEl = null;
            try
            {
                var prose = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (prose.Count > 1) contentEl = prose[1];
                contentEl ??= await _page.QuerySelectorAsync(".ProseMirror.hb-editor");

                if (contentEl != null)
                {
                    await contentEl.ClickAsync();
                    await Task.Delay(300);

                    // 遍历分段内容：交替输入文本和插入图片
                    foreach (var (text, imagePath) in contentSegments)
                    {
                        // 若有文本，先输入文本
                        if (!string.IsNullOrEmpty(text))
                        {
                            await contentEl.ClickAsync();
                            await Task.Delay(100);
                            await _page.Keyboard.TypeAsync(text);
                            await Task.Delay(200);
                            _logger.LogInformation("[Article] 已填写正文文本段落");
                        }

                        // 若有图片路径，插入图片
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            await contentEl.ClickAsync(); // 确保聚焦以展示按钮
                            await Task.Delay(200);

                            var imgBtn = await _page.QuerySelectorAsync("button.editor-menu-image__btn, .editor-menu-image__btn");
                            if (imgBtn != null && await imgBtn.IsVisibleAsync())
                            {
                                await imgBtn.ClickAsync();
                                await Task.Delay(500);

                                // 查找上传框：.hb-cpt__input-box 内的 input.hb-cpt__input-item
                                var uploadInput = await _page.QuerySelectorAsync(".hb-cpt__input-box input.hb-cpt__input-item");
                                // 同时查找本地上传按钮：.editor-image-wrapper__box.upload
                                var uploadBtn = await _page.QuerySelectorAsync(".editor-image-wrapper__box.upload");
                                // 确认按钮
                                var confirmBtn = await _page.QuerySelectorAsync("button.editor-__model-frame-bottom-btn.hb-color__btn--confirm");

                                if (uploadBtn != null && await uploadBtn.IsVisibleAsync())
                                {
                                    // 本地文件路径：点击上传按钮打开文件选择器
                                    var chooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                                    {
                                        await uploadBtn.ClickAsync();
                                    });

                                    if (chooser != null && confirmBtn != null)
                                    {
                                        await chooser.SetFilesAsync(imagePath);
                                        await Task.Delay(2000);
                                        
                                        await confirmBtn.ClickAsync();
                                        await Task.Delay(1000);
                                        
                                        _logger.LogInformation($"[Article] 已插入图片(本地): {imagePath}");
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"[Article] 未能打开文件选择器，图片: {imagePath}");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("[Article] 未找到上传框或上传按钮");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("[Article] 未找到图片按钮 .editor-menu-image__btn");
                            }
                        }
                    }

                    // 若allImages还有额外的非嵌入图片，继续上传
                    var embeddedPaths = contentSegments
                        .Where(s => !string.IsNullOrEmpty(s.ImagePath))
                        .Select(s => s.ImagePath!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var additionalImages = allImages.Where(p => !embeddedPaths.Contains(p)).ToList();

                    if (additionalImages.Count > 0)
                    {
                        _logger.LogInformation($"[Article] 准备上传额外图片: {additionalImages.Count} 张");
                        foreach (var imgPath in additionalImages)
                        {
                            await contentEl.ClickAsync();
                            await Task.Delay(200);

                            var imgBtn = await _page.QuerySelectorAsync("button.editor-menu-image__btn, .editor-menu-image__btn");
                            if (imgBtn != null && await imgBtn.IsVisibleAsync())
                            {
                                await imgBtn.ClickAsync();
                                await Task.Delay(500);

                                var uploadInput = await _page.QuerySelectorAsync(".hb-cpt__input-box input.hb-cpt__input-item");
                                var uploadBtn = await _page.QuerySelectorAsync(".editor-image-wrapper__box.upload");

                                if (uploadBtn != null && await uploadBtn.IsVisibleAsync())
                                {
                                    var chooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                                    {
                                        await uploadBtn.ClickAsync();
                                    });

                                    if (chooser != null)
                                    {
                                        await chooser.SetFilesAsync(imgPath);
                                        await Task.Delay(2000);
                                        
                                        // 点击确定按钮确认上传
                                        var confirmBtn = await _page.QuerySelectorAsync("button.editor-__model-frame-bottom-btn.hb-color__btn--confirm");
                                        if (confirmBtn != null)
                                        {
                                            await confirmBtn.ClickAsync();
                                            await Task.Delay(1000);
                                        }
                                        
                                        _logger.LogInformation($"[Article] 已插入额外图片(本地): {imgPath}");
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInformation("[Article] 正文与图片已完成交替填充");
                }
                else
                {
                    _logger.LogWarning("[Article] 未找到正文 ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Article] 填写正文及图片失败");
            }

            // 4) 社区与话题与图文一致
            var published = await CommonService.SelectCommunityAndTag(new CommonArgs(args.Communities, args.Tags), _page, _logger);

            await Task.Delay(1000);
            await SaveCookiesAsync();

            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = published
                        ? $"✅ 文章发布成功！\n标题: {args.Title}"
                        : $"⚠️ 文章内容已填写，但未能自动点击发布按钮，请手动检查并发布\n标题: {args.Title}" }
                ]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Article] 发布文章失败");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"❌ 发布文章失败: {ex.Message}" }
                ],
                IsError = true
            };
        }
    }

    /// <summary>
    /// 解析正文：将含有绝对路径的图片与文本分段
    /// 返回分段列表：(Text, ImagePath?) - 如果ImagePath非null则为图片，否则为文本
    /// </summary>
    private (List<(string Text, string? ImagePath)> Segments, List<string> AllImages) ParseContentWithImages(string content)
    {
        var segments = new List<(string Text, string? ImagePath)>();
        var allImages = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
            return (segments, allImages);

        var exts = "png|jpg|jpeg|gif|webp|bmp";
        var winPattern = $@"[a-zA-Z]:\\[^\r\n""']+?\.({exts})";
        var posixPattern = $@"/(?:[^\s""']+/)*[^\s""']+\.({exts})";

        var matches = Regex.Matches(content, winPattern)
            .Cast<Match>()
            .Concat(Regex.Matches(content, posixPattern).Cast<Match>())
            .OrderBy(m => m.Index)
            .ToList();

        if (matches.Count == 0)
        {
            // 无图片，整个内容作为一个分段
            segments.Add((content, null));
            return (segments, allImages);
        }

        var lastPos = 0;
        foreach (var match in matches)
        {
            // 匹配前的文本
            var textBefore = content[lastPos..match.Index].Trim();
            if (!string.IsNullOrEmpty(textBefore))
            {
                segments.Add((textBefore, null));
            }

            // 图片
            var imagePath = match.Value;
            segments.Add((string.Empty, imagePath));
            if (!allImages.Contains(imagePath, StringComparer.OrdinalIgnoreCase))
            {
                allImages.Add(imagePath);
            }

            lastPos = match.Index + match.Length;
        }

        // 最后的文本
        var textAfter = content[lastPos..].Trim();
        if (!string.IsNullOrEmpty(textAfter))
        {
            segments.Add((textAfter, null));
        }

        return (segments, allImages);
    }
}
