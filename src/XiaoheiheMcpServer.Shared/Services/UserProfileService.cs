using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using XiaoheiheMcpServer.Shared.Models;

namespace XiaoheiheMcpServer.Shared.Services;

public class UserProfileService : BrowserBase
{
    public UserProfileService(ILogger<UserProfileService> logger, bool headless = true) 
        : base(logger, headless)
    {
    }
    
    private readonly string _profileUrl = "https://xiaoheihe.cn/app/user/profile";
    /// <summary>
    /// 获取用户个人信息（需要登录状态）
    /// </summary> <returns>用户的动态</returns>
    public async Task<object> GetUserProfileAsync(int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("获取用户个人信息...");
            await InitializeBrowserAsync();
            
            _logger.LogInformation("访问个人信息页: {Url}", _profileUrl);
            await _page!.GotoAsync(_profileUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var username = await _page.Locator("div.name").TextContentAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            // 获取用户动态内容（示例：获取动态列表的HTML）
            var listContainer = _page.Locator("div.user-profile__list");
            await listContainer.WaitForAsync();
            var linkItems = listContainer.Locator("a[href*='/app/bbs/link/']");
            var linkCount = await linkItems.CountAsync();
            _logger.LogInformation("找到 {Count} 条动态", linkCount);
            var results = new List<SearchResultItem>();
            var maxCount = Math.Min(pageSize, Math.Min(linkCount, 20));
            for (var i = 0; i < maxCount; i++)
            {
                var linkItem = linkItems.Nth(i);
                var href = await linkItem.GetAttributeAsync("href") ?? "";
                var postId = InteractionService.ExtractPostId(href ?? "");

                if (string.IsNullOrEmpty(postId)) continue;

                var item = linkItem.Locator("xpath=ancestor::*[contains(@class,'content-list__item')][1]");
                var hasItem = await item.CountAsync() > 0;
                var scope = hasItem ? item : linkItem;

                // 获取标题：div.bbs-content__title 内的文本（包含emoji）
                var titleLocator = scope.Locator("div.bbs-content__title");
                var title = await titleLocator.CountAsync() > 0
                    ? await titleLocator.First.TextContentAsync()
                    : "无标题";

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
                        Link = postId, // 只保存帖子ID
                        CommentCount = commentCount,
                        LikeCount = likeCount
                    });
            }
            return new
            {
                UserName = username?.Trim() ?? "未知用户",
                Posts = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户个人信息时发生错误");
            return $"获取个人信息失败: {ex.Message}";
        }
    }

}