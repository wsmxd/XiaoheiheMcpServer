using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace XiaoheiheMcpServer.Services;

/// <summary>
/// 小黑盒内容发布服务 - 处理图文、文章、视频发布
/// </summary>
public class PublishService : BrowserBase
{
    public PublishService(ILogger<PublishService> logger, bool headless = true)
        : base(logger, headless)
    {
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

            // 访问图文编辑器页面
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/image_text");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 填写标题
            var titleSelector = "input[placeholder*='标题'], [class*='title'] input, input[name*='title']";
            await _page.WaitForSelectorAsync(titleSelector, new() { Timeout = 10000 });
            await _page.FillAsync(titleSelector, args.Title);
            await Task.Delay(500);

            // 填写内容
            var contentSelector = "textarea[placeholder*='内容'], [contenteditable='true'], div[class*='editor'], div[class*='content']";
            var contentText = args.Content;
            
            if (args.Tags.Any())
            {
                contentText += "\n" + string.Join(" ", args.Tags.Select(t => $"#{t}"));
            }
            
            // 尝试填充内容（支持多种编辑器类型）
            try
            {
                await _page.FillAsync(contentSelector, contentText);
            }
            catch
            {
                // 如果是 contenteditable 元素，使用点击+输入的方式
                await _page.ClickAsync(contentSelector);
                await _page.Keyboard.TypeAsync(contentText);
            }
            await Task.Delay(500);

            // 上传图片
            if (args.Images.Any())
            {
                await UploadImagesAsync(args.Images.ToArray());
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
                Content =
                [
                    new() { Type = "text", Text = $"✅ 发布成功！\n标题: {args.Title}\n内容已发布到小黑盒" }
                ]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布内容失败");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"❌ 发布失败: {ex.Message}" }
                ],
                IsError = true
            };
        }
    }

    /// <summary>
    /// 发布文章（article，长文章形式）
    /// </summary>
    public async Task<McpToolResult> PublishArticleAsync(PublishArticleArgs args)
    {
        try
        {
            _logger.LogInformation($"开始发布文章: {args.Title}");
            await InitializeBrowserAsync();

            // 访问文章编辑器页面（article，不是image_text）
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/article");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 填写标题
            var titleSelector = "input[placeholder*='标题'], [class*='title'] input, input[name*='title']";
            await _page.WaitForSelectorAsync(titleSelector, new() { Timeout = 10000 });
            await _page.FillAsync(titleSelector, args.Title);
            await Task.Delay(500);

            // 填写内容（文章编辑器通常使用富文本编辑器）
            var contentSelector = "div[contenteditable='true'], [class*='editor-content'], [class*='rich-editor'], textarea";
            var contentText = args.Content;
            
            if (args.Tags.Any())
            {
                contentText += "\n" + string.Join(" ", args.Tags.Select(t => $"#{t}"));
            }
            
            // 文章编辑器通常是contenteditable的div
            try
            {
                var editorElement = await _page.WaitForSelectorAsync(contentSelector, new() { Timeout = 5000 });
                await editorElement.ClickAsync(); // 聚焦编辑器
                await Task.Delay(300);
                
                // 使用键盘输入以保持格式
                await _page.Keyboard.TypeAsync(contentText);
            }
            catch
            {
                // 备用方案：直接填充
                await _page.FillAsync(contentSelector, contentText);
            }
            await Task.Delay(500);

            // 上传图片
            if (args.Images.Any())
            {
                await UploadImagesAsync(args.Images.ToArray());
            }

            // 点击发布按钮
            var publishSelector = "button[class*='publish'], button:has-text('发布'), button:has-text('提交')";
            await _page.WaitForSelectorAsync(publishSelector);
            await _page.ClickAsync(publishSelector);
            await Task.Delay(3000);

            await SaveCookiesAsync();

            _logger.LogInformation("文章发布成功");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"✅ 文章发布成功！\n标题: {args.Title}\n文章已发布到小黑盒" }
                ]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布文章失败");
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
    /// 发布视频
    /// </summary>
    public async Task<McpToolResult> PublishVideoAsync(PublishVideoArgs args)
    {
        try
        {
            _logger.LogInformation($"开始发布视频: {args.Title}");
            
            if (!File.Exists(args.VideoPath))
            {
                return new McpToolResult
                {
                    Content =
                    [
                        new() { Type = "text", Text = $"❌ 视频文件不存在: {args.VideoPath}" }
                    ],
                    IsError = true
                };
            }

            await InitializeBrowserAsync();

            // 访问视频编辑器页面
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/video");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 上传视频文件
            _logger.LogInformation("准备上传视频文件...");
            var videoInputSelectors = new[]
            {
                "input[type='file'][accept*='video']", // 视频文件输入
                "input[type='file']", // 通用文件输入
                "[class*='video-upload'] input[type='file']" // 视频上传组件
            };

            IElementHandle? videoInput = null;
            foreach (var selector in videoInputSelectors)
            {
                try
                {
                    videoInput = await _page.QuerySelectorAsync(selector);
                    if (videoInput != null)
                    {
                        _logger.LogInformation($"找到视频文件输入元素，选择器: {selector}");
                        break;
                    }
                }
                catch { }
            }

            if (videoInput == null)
            {
                return new McpToolResult
                {
                    Content =
                    [
                        new() { Type = "text", Text = "❌ 未找到视频上传控件" }
                    ],
                    IsError = true
                };
            }

            // 上传视频
            _logger.LogInformation("开始上传视频...");
            await videoInput.SetInputFilesAsync(args.VideoPath);
            
            // 等待视频上传（视频文件通常较大，需要更长时间）
            _logger.LogInformation("等待视频上传完成（可能需要较长时间）...");
            await Task.Delay(30000); // 等待30秒基础时间
            
            // 等待上传进度条消失或完成提示出现
            try
            {
                await _page.WaitForSelectorAsync("[class*='upload-success'], [class*='upload-complete']", 
                    new() { Timeout = 300000 }); // 最多等待5分钟
            }
            catch
            {
                _logger.LogWarning("未检测到上传完成标志，继续执行");
            }

            // 填写标题
            var titleSelector = "input[placeholder*='标题'], [class*='title'] input, input[name*='title']";
            try
            {
                await _page.WaitForSelectorAsync(titleSelector, new() { Timeout = 10000 });
                await _page.FillAsync(titleSelector, args.Title);
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"填写标题失败: {ex.Message}");
            }

            // 填写描述
            var descriptionSelector = "textarea[placeholder*='描述'], textarea[placeholder*='简介'], [class*='description'] textarea";
            try
            {
                var descriptionText = args.Description;
                if (args.Tags.Any())
                {
                    descriptionText += "\n" + string.Join(" ", args.Tags.Select(t => $"#{t}"));
                }
                
                await _page.FillAsync(descriptionSelector, descriptionText);
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"填写描述失败: {ex.Message}");
            }

            // 上传封面图（如果提供）
            if (!string.IsNullOrEmpty(args.CoverImagePath) && File.Exists(args.CoverImagePath))
            {
                _logger.LogInformation("准备上传封面图...");
                try
                {
                    var coverInputSelectors = new[]
                    {
                        "input[type='file'][accept*='image']",
                        "[class*='cover'] input[type='file']",
                        "[class*='poster'] input[type='file']"
                    };

                    IElementHandle? coverInput = null;
                    foreach (var selector in coverInputSelectors)
                    {
                        try
                        {
                            var elements = await _page.QuerySelectorAllAsync(selector);
                            // 跳过已经使用的视频输入框
                            coverInput = elements.FirstOrDefault(e => e != videoInput);
                            if (coverInput != null)
                            {
                                _logger.LogInformation($"找到封面图输入元素，选择器: {selector}");
                                break;
                            }
                        }
                        catch { }
                    }

                    if (coverInput != null)
                    {
                        await coverInput.SetInputFilesAsync(args.CoverImagePath);
                        await Task.Delay(3000);
                        _logger.LogInformation("封面图上传完成");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"上传封面图失败: {ex.Message}");
                }
            }

            // 点击发布按钮
            var publishSelector = "button[class*='publish'], button:has-text('发布'), button:has-text('提交')";
            await _page.WaitForSelectorAsync(publishSelector);
            await _page.ClickAsync(publishSelector);
            await Task.Delay(3000);

            await SaveCookiesAsync();

            _logger.LogInformation("视频发布成功");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"✅ 视频发布成功！\n标题: {args.Title}\n视频已发布到小黑盒" }
                ]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布视频失败");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = $"❌ 发布视频失败: {ex.Message}" }
                ],
                IsError = true
            };
        }
    }

    /// <summary>
    /// 上传图片到编辑器 - 提取的公共逻辑
    /// </summary>
    private async Task UploadImagesAsync(string[] imagePaths)
    {
        var validImages = imagePaths.Where(File.Exists).ToArray();
        if (!validImages.Any())
        {
            _logger.LogWarning("未找到有效的图片文件");
            return;
        }

        _logger.LogInformation($"准备上传 {validImages.Length} 张图片");

        // 1. 先查找隐藏的 file input 元素（可能页面上已存在）
        IElementHandle? fileInput = null;
        var fileInputSelectors = new[]
        {
            "input[type='file'][accept*='image']", // 专门的图片文件输入
            "input[type='file']" // 通用文件输入
        };

        foreach (var selector in fileInputSelectors)
        {
            try
            {
                var inputs = await _page!.QuerySelectorAllAsync(selector);
                // 找到第一个可用的文件输入框
                fileInput = inputs.FirstOrDefault();
                if (fileInput != null)
                {
                    _logger.LogInformation($"找到文件输入元素: {selector}");
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"查找文件输入 {selector} 失败: {ex.Message}");
            }
        }

        // 2. 如果没有找到file input，尝试点击工具栏中的图片按钮来触发
        if (fileInput == null)
        {
            _logger.LogInformation("未找到现有file input，尝试点击图片按钮...");
            
            var imageButtonSelectors = new[]
            {
                "[class*='toolbar'] svg", // 工具栏中的SVG图标
                "[class*='editor-toolbar'] svg",
                "[class*='bottom'] svg", // 底部的SVG图标
                "svg[class*='icon']", // 图标SVG
                "[title*='图片']",
                "[title*='照片']",
                "[aria-label*='图片']",
                "[aria-label*='image']",
                "button svg, i svg",
                "[class*='image-btn']",
                "[class*='photo-btn']",
                "[class*='picture-btn']"
            };

            bool buttonFound = false;
            foreach (var selector in imageButtonSelectors)
            {
                try
                {
                    var elements = await _page.QuerySelectorAllAsync(selector);
                    foreach (var element in elements)
                    {
                        if (await element.IsVisibleAsync())
                        {
                            try
                            {
                                var parent = await element.EvaluateHandleAsync("el => el.closest('button, div[role=\"button\"], a')");
                                if (parent != null)
                                {
                                    var clickableElement = parent.AsElement();
                                    if (clickableElement != null)
                                    {
                                        _logger.LogInformation($"找到可点击的图片按钮: {selector}");
                                        await clickableElement.ClickAsync();
                                        await Task.Delay(1000);
                                        buttonFound = true;
                                        
                                        // 点击后重新查找file input
                                        fileInput = await _page.QuerySelectorAsync("input[type='file']");
                                        if (fileInput != null)
                                        {
                                            _logger.LogInformation("点击按钮后找到file input");
                                            break;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    if (buttonFound && fileInput != null) break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"尝试图片按钮选择器 {selector} 失败: {ex.Message}");
                }
            }
        }

        // 3. 上传文件
        if (fileInput != null)
        {
            _logger.LogInformation("开始上传图片文件...");
            try
            {
                await fileInput.SetInputFilesAsync(validImages);
                
                // 等待上传完成（每张图片3秒 + 基础2秒）
                var uploadTime = 2000 + (3000 * validImages.Length);
                _logger.LogInformation($"等待 {uploadTime}ms 让图片上传完成");
                await Task.Delay(uploadTime);
                
                _logger.LogInformation("图片上传完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传图片文件时出错");
                throw;
            }
        }
        else
        {
            _logger.LogWarning("未找到文件上传控件，跳过图片上传");
            _logger.LogWarning("提示：可能需要手动调整选择器以匹配实际页面结构");
        }
    }
}
