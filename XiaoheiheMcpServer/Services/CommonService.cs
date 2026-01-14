using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using XiaoheiheMcpServer.Models;

public class CommonService
{
    internal static async Task<bool> SelectCommunityAndTag(CommonArgs args, IPage _page, ILogger _logger)
    {
        if (args.Communities.Count != 0)
            {
                _logger.LogInformation($"开始选择社区，共{args.Communities.Count}个...");
                try
                {
                    foreach (var communityName in args.Communities)
                    {
                        var addButtons = await _page.QuerySelectorAllAsync(".editor__add-btn");
                        if (addButtons.Count > 0)
                        {
                            // 每次都点击添加按钮（社区）
                            await addButtons[0].ClickAsync();
                            await Task.Delay(1000);
                            _logger.LogInformation($"点击添加社区按钮，搜索: {communityName}");
                            
                            // 输入社区名称
                            var searchInput = await _page.QuerySelectorAsync(".editor__search-input--input");
                            if (searchInput != null)
                            {
                                await searchInput.ClickAsync();
                                await Task.Delay(200);
                                await searchInput.FillAsync(communityName);
                                await Task.Delay(1400);
                                
                                // 点击editor-model__topic-list下的第一个社区项
                                var communityItem = await _page.QuerySelectorAsync(".editor-model__topic-list .editor-model__topic-list-item");
                                if (communityItem != null)
                                {
                                    await communityItem.ClickAsync();
                                    await Task.Delay(500);
                                    _logger.LogInformation($"已选择社区: {communityName}");
                                }
                                else
                                {
                                    _logger.LogWarning($"未找到社区: {communityName}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "选择社区失败");
                }
            }

            // 5. 添加话题 - 点击第二个添加按钮
            if (args.Tags.Count != 0)
            {
                _logger.LogInformation($"开始添加话题，共{args.Tags.Count}个...");
                try
                {
                    foreach (var tag in args.Tags)
                    {
                        // 话题按钮位于 .editor__more-info.hashtags 容器内
                        var hashtagAddBtn = await _page.QuerySelectorAsync(".editor__more-info.hashtags .editor__add-btn");
                        if (hashtagAddBtn != null)
                        {
                            // 每次都点击话题按钮
                            await hashtagAddBtn.ClickAsync();
                            await Task.Delay(1000);
                            _logger.LogInformation($"点击添加话题按钮，搜索: {tag}");
                            
                            // 输入话题名称
                            var searchInput = await _page.QuerySelectorAsync(".editor__search-input--input");
                            if (searchInput != null)
                            {
                                await searchInput.ClickAsync();
                                await Task.Delay(200);
                                await searchInput.FillAsync(tag);
                                await Task.Delay(800);
                                
                                // 点击第一个话题
                                var tagItem = await _page.QuerySelectorAsync(".editor-model__hashtag-list-item");
                                if (tagItem != null)
                                {
                                    await tagItem.ClickAsync();
                                    await Task.Delay(500);
                                    _logger.LogInformation($"已添加话题: {tag}");
                                }
                                else
                                {
                                    _logger.LogWarning($"未找到话题: {tag}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "添加话题失败");
                }
            }

            // 6. 查找并点击发布按钮
            bool published = false;
            var pBtn = await _page.QuerySelectorAsync(".editor-publish__btn.main-btn");
            if (pBtn != null)
            {
                _logger.LogInformation("找到发布按钮，准备点击...");
                await pBtn.ClickAsync();
                published = true;
            }
            return published;
    }
}