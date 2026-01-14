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

            _logger.LogInformation($"开始发布内容: {args.Title}");
            await InitializeBrowserAsync();

            // 访问图文编辑器页面
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/image_text");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 1. 上传图片 - 点击 editor-image-wrapper__box upload 区域
            if (args.Images.Any())
            {
                _logger.LogInformation("上传封面图片...");
                var validImages = args.Images.Where(File.Exists).ToArray();
                if (validImages.Any())
                {
                    try
                    {
                        // 先查找隐藏的文件输入框
                        var fileInput = await _page.QuerySelectorAsync("input[type='file']");
                        
                        if (fileInput != null)
                        {
                            _logger.LogInformation("直接找到文件输入框，准备上传...");
                            await fileInput.SetInputFilesAsync(validImages);
                            await Task.Delay(2000 + (1000 * validImages.Length));
                            _logger.LogInformation($"已上传 {validImages.Length} 张图片");
                        }
                        else
                        {
                            // 如果没找到，点击上传图片区域触发
                            _logger.LogInformation("未找到文件输入框，尝试点击上传区域...");
                            var uploadBox = await _page.QuerySelectorAsync(".editor-image-wrapper__box.upload");
                            
                            if (uploadBox != null)
                            {
                                _logger.LogInformation("找到图片上传区域，点击触发文件选择器...");
                                
                                // 使用 RunAndWaitForFileChooserAsync 来处理文件选择
                                var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                                {
                                    await uploadBox.ClickAsync();
                                });
                                
                                if (fileChooser != null)
                                {
                                    _logger.LogInformation($"文件选择器已打开，设置文件: {string.Join(", ", validImages)}");
                                    await fileChooser.SetFilesAsync(validImages);
                                    await Task.Delay(2000 + (1000 * validImages.Length));
                                    _logger.LogInformation($"已上传 {validImages.Length} 张图片");
                                }
                                else
                                {
                                    _logger.LogWarning("点击后未能获取文件选择器");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("未找到图片上传区域 .editor-image-wrapper__box.upload");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "上传图片失败");
                    }
                }
            }

            // 2. 填写标题 - 找到标题输入框并点击输入
            _logger.LogInformation("开始填写标题...");
            try
            {
                // 找到所有 ProseMirror 编辑器
                var proseMirrors = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (proseMirrors.Count > 0)
                {
                    // 第一个 ProseMirror 是标题
                    var titleInput = proseMirrors[0];
                    _logger.LogInformation("找到标题输入框，点击聚焦...");
                    
                    // 点击聚焦
                    await titleInput.ClickAsync();
                    await Task.Delay(300);
                    
                    // 全选并删除原有内容
                    await _page.Keyboard.PressAsync("Control+A");
                    await Task.Delay(100);
                    await _page.Keyboard.PressAsync("Delete");
                    await Task.Delay(300);
                    
                    // 输入标题
                    await _page.Keyboard.TypeAsync(args.Title);
                    await Task.Delay(500);
                    _logger.LogInformation("标题已填写");
                }
                else
                {
                    _logger.LogWarning("未找到标题输入框 .ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "填写标题失败");
            }

            // 3. 填写正文 - 找到正文输入框并点击输入
            _logger.LogInformation("开始填写正文...");
            try
            {
                // 找到所有 ProseMirror 编辑器，第二个是正文
                var proseMirrors = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (proseMirrors.Count > 1)
                {
                    // 第二个 ProseMirror 是正文
                    var contentInput = proseMirrors[1];
                    _logger.LogInformation("找到正文输入框，点击聚焦...");
                    
                    // 点击聚焦
                    await contentInput.ClickAsync();
                    await Task.Delay(300);
                    
                    // 全选并删除原有内容
                    await _page.Keyboard.PressAsync("Control+A");
                    await Task.Delay(100);
                    await _page.Keyboard.PressAsync("Delete");
                    await Task.Delay(300);
                    
                    // 输入正文
                    await _page.Keyboard.TypeAsync(args.Content);
                    await Task.Delay(1000);
                    _logger.LogInformation("正文已填写");
                }
                else
                {
                    _logger.LogWarning("未找到第二个正文输入框 .ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "填写正文失败");
            }

            // 4. 选择社区 - 点击第一个添加按钮
            var published = await CommonService.SelectCommunityAndTag(new CommonArgs(args.Communities, args.Tags), _page, _logger);

            await Task.Delay(3000);
            await SaveCookiesAsync();

            _logger.LogInformation("内容发布操作完成");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = published
                        ? $"✅ 发布成功！\n标题: {args.Title}\n内容已发布到小黑盒" 
                        : $"⚠️ 内容已填写，但未能自动点击发布按钮，请手动检查并发布\n标题: {args.Title}" }
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
                    new() { Type = "text", Text = $"❌ 发布失败: {ex.Message}\n\n建议：\n1. 确保已登录\n2. 检查图片文件路径是否正确\n3. 尝试手动访问发布页面确认页面结构" }
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

            if (string.IsNullOrWhiteSpace(args.VideoPath) || !File.Exists(args.VideoPath))
            {
                return new McpToolResult
                {
                    IsError = true,
                    Content =
                    [
                        new() { Type = "text", Text = $"❌ 视频文件不存在: {args.VideoPath}" }
                    ]
                };
            }

            _logger.LogInformation($"开始发布视频: {args.Title}");
            await InitializeBrowserAsync();

            // 访问视频编辑器页面
            await _page!.GotoAsync($"{BaseUrl}/creator/editor/draft/video");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // 1. 上传视频 - 点击 video-uploader__unload 按钮
            _logger.LogInformation("准备上传视频文件...");
            try
            {
                var uploadButton = await _page.QuerySelectorAsync(".video-uploader__unload");
                
                if (uploadButton != null)
                {
                    _logger.LogInformation("找到视频上传按钮，点击触发文件选择器...");
                    
                    // 使用 RunAndWaitForFileChooserAsync 来处理文件选择
                    var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                    {
                        await uploadButton.ClickAsync();
                    });
                    
                    if (fileChooser != null)
                    {
                        _logger.LogInformation($"文件选择器已打开，设置视频文件: {args.VideoPath}");
                        await fileChooser.SetFilesAsync(args.VideoPath);
                        _logger.LogInformation("视频文件已选择，等待上传...");
                        
                        // 等待视频上传
                        await Task.Delay(7000);
                    }
                    else
                    {
                        _logger.LogWarning("点击后未能获取文件选择器");
                    }
                }
                else
                {
                    _logger.LogWarning("未找到视频上传按钮 .video-uploader__unload");
                    return new McpToolResult
                    {
                        IsError = true,
                        Content =
                        [
                            new() { Type = "text", Text = "❌ 未找到视频上传按钮" }
                        ]
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传视频失败");
                return new McpToolResult
                {
                    IsError = true,
                    Content =
                    [
                        new() { Type = "text", Text = $"❌ 上传视频失败: {ex.Message}" }
                    ]
                };
            }

            // 2. 上传封面图片
            _logger.LogInformation("准备上传封面图片...");
            try
            {
                var updateCoverButton = await _page.QuerySelectorAsync(".video-uploader__loaded-operation-btn");
                if (updateCoverButton != null && !string.IsNullOrWhiteSpace(args.CoverImagePath))
                {
                    await updateCoverButton.ClickAsync();
                    await Task.Delay(400);

                    var uploadCoverElement = await _page.QuerySelectorAsync(".editor-model__thumb-upload");
                    if (uploadCoverElement != null)
                    {
                        var coverImageChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
                        {
                            await uploadCoverElement.ClickAsync();
                        });
                        
                        if (coverImageChooser != null)
                        {
                            await coverImageChooser.SetFilesAsync(args.CoverImagePath);
                            _logger.LogInformation("封面图片已选择，等待上传...");
                            await Task.Delay(600);
                            var confirmCoverBtn = await _page.QuerySelectorAsync(".editor-__model-frame-bottom-btn.hb-color__btn--confirm");
                            if (confirmCoverBtn != null)
                            {
                                await confirmCoverBtn.ClickAsync();
                                await Task.Delay(2000);
                                _logger.LogInformation("封面图片已上传");
                            }
                            else
                            {
                                _logger.LogWarning("未找到封面图片确认按钮");
                                // 创建一个日志文件
                                File.AppendAllText("upload_cover_warning.log", $"未找到封面图片确认按钮, 时间{DateTime.Now}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("未能获取文件选择器");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("未找到封面图片上传元素 .editor-model__thumb-upload");
                    }
                }
                else
                {
                    _logger.LogWarning("未找到封面图片编辑按钮 .video-uploader__loaded-operation-btn");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传封面图片失败");
            }

            // 3. 填写标题 - 找到标题输入框并点击输入
            _logger.LogInformation("开始填写标题...");
            try
            {
                // 找到所有 ProseMirror 编辑器
                var proseMirrors = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (proseMirrors.Count > 0)
                {
                    // 第一个 ProseMirror 是标题
                    var titleInput = proseMirrors[0];
                    _logger.LogInformation("找到标题输入框，点击聚焦...");
                    
                    // 点击聚焦
                    await titleInput.ClickAsync();
                    await Task.Delay(300);
                    
                    // 全选并删除原有内容
                    await _page.Keyboard.PressAsync("Control+A");
                    await Task.Delay(100);
                    await _page.Keyboard.PressAsync("Delete");
                    await Task.Delay(300);
                    
                    // 输入标题
                    await _page.Keyboard.TypeAsync(args.Title);
                    await Task.Delay(500);
                    _logger.LogInformation("标题已填写");
                }
                else
                {
                    _logger.LogWarning("未找到标题输入框 .ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "填写标题失败");
            }

            // 4. 填写正文 - 找到正文输入框并点击输入
            _logger.LogInformation("开始填写正文...");
            try
            {
                // 找到所有 ProseMirror 编辑器，第二个是正文
                var proseMirrors = await _page.QuerySelectorAllAsync(".ProseMirror");
                if (proseMirrors.Count > 1)
                {
                    // 第二个 ProseMirror 是正文
                    var contentInput = proseMirrors[1];
                    _logger.LogInformation("找到正文输入框，点击聚焦...");
                    
                    // 点击聚焦
                    await contentInput.ClickAsync();
                    await Task.Delay(300);
                    
                    // 全选并删除原有内容
                    await _page.Keyboard.PressAsync("Control+A");
                    await Task.Delay(100);
                    await _page.Keyboard.PressAsync("Delete");
                    await Task.Delay(300);
                    
                    // 输入正文
                    await _page.Keyboard.TypeAsync(args.Content);
                    await Task.Delay(1000);
                    _logger.LogInformation("正文已填写");
                }
                else
                {
                    _logger.LogWarning("未找到第二个正文输入框 .ProseMirror");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "填写正文失败");
            }

            // 5. 选择社区 - 点击第一个添加按钮
            var published = await CommonService.SelectCommunityAndTag(new CommonArgs(args.Communities, args.Tags), _page, _logger);

            await Task.Delay(3000);
            await SaveCookiesAsync();

            _logger.LogInformation("视频发布操作完成");
            return new McpToolResult
            {
                Content =
                [
                    new() { Type = "text", Text = published
                        ? $"✅ 视频发布成功！\n标题: {args.Title}\n视频已发布到小黑盒" 
                        : $"⚠️ 内容已填写，但未能自动点击发布按钮，请手动检查并发布\n标题: {args.Title}" }
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
                    new() { Type = "text", Text = $"❌ 发布视频失败: {ex.Message}\n\n建议：\n1. 确保已登录\n2. 检查视频文件路径是否正确\n3. 确保视频格式符合平台要求\n4. 尝试手动访问发布页面确认页面结构" }
                ],
                IsError = true
            };
        }
    }
}
