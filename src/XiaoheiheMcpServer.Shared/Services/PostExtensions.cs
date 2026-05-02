namespace XiaoheiheMcpServer.Shared.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using XiaoheiheMcpServer.Shared.Models;

public static class PostExtensions
{
    
    extension(IPage page)
    {
        /// <summary>
        /// 点击回复评论按钮，触发回复输入框
        /// </summary>
        /// <param name="targetContent">目标评论内容（用于定位）</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>是否成功找到并点击了回复按钮</returns>
        public async Task<bool> ClickReplyCommentAsync(string targetContent, ILogger logger)
        {
            // 方式1: 尝试直接点击评论上的回复按钮（更可靠）
            var commentItems = await page.QuerySelectorAllAsync(".link-comment__comment-item");
            logger.LogInformation("找到 {Count} 条评论，准备查找目标评论", commentItems.Count);
            
            foreach (var commentItem in commentItems)
            {
                var contentElem = await commentItem.QuerySelectorAsync(".comment-item__content");
                if (contentElem == null) continue;
                
                var textContent = await contentElem.TextContentAsync();
                if (textContent == null || !textContent.Trim().Contains(targetContent.Trim()))
                    continue;
                
                logger.LogInformation("找到目标评论: {Content}", textContent.Trim().Substring(0, Math.Min(50, textContent.Trim().Length)));
                
                // 先尝试点击评论元素上的回复按钮（有些网站评论项上有直接的回复按钮）
                var directReplyBtn = await commentItem.QuerySelectorAsync("[class*='reply-btn'], [class*='replyBtn'], button:has-text('回复')");
                if (directReplyBtn != null && await directReplyBtn.IsVisibleAsync())
                {
                    await directReplyBtn.ClickAsync();
                    await Task.Delay(500);
                    logger.LogInformation("直接点击回复按钮成功");
                    return true;
                }
                
                // 方式2: 使用右键点击评论触发上下文菜单
                await contentElem.ClickAsync(new ElementHandleClickOptions
                {
                    Button = MouseButton.Right
                });
                await Task.Delay(800); // 等待右键菜单出现
                
                // 获取右键菜单中的回复按钮
                var replyButton = page.Locator("div.hb-cpt__popover-menu-item.plain", new PageLocatorOptions
                {
                    HasTextString = "回复评论"
                });
                
                if (await replyButton.CountAsync() > 0 && await replyButton.First.IsVisibleAsync())
                {
                    await replyButton.First.ClickAsync();
                    await Task.Delay(500);
                    logger.LogInformation("通过右键菜单点击回复按钮成功");
                    return true;
                }
                
                logger.LogWarning("找到评论但未能点击回复按钮");
                return false;
            }
            
            logger.LogWarning("未找到内容匹配的评论: {Content}", targetContent);
            return false;
        }
    }
    extension<T1, T2, T3>(Func<T1, T2>)
    {
        public static Func<T1, T3> operator <<(Func<T1, T2> f1, Func<T2, T3> f2)
        {
            return x => f2(f1(x));
        }
    }

    extension<T1, T2, T3>(Func<T1, Task<T2>>)
    {
        public static Func<T1, Task<T3>> operator <<(Func<T1, Task<T2>> f1, Func<T2, Task<T3>> f2)
        {
            return async x => await f2(await f1(x));
        }
    }

    extension<T1, T2, T3>(Func<T1, Task<T2>>)
    {
        public static Func<T1, Task<T3>> operator <<(Func<T1, Task<T2>> f1, Func<T2, T3> f2)
        {
            return async x => f2(await f1(x));
        }
    }

    extension<T1, T2>(Func<T1, Task<T2>> f1)
    {
        public Func<T1, Task<T3>> ComposeAsync<T3>(Func<T2, Task<T3>> f2)
        {
            return async x => await f2(await f1(x));
        }

        public Func<T1, Task<T3>> ComposeAsync<T3>(Func<T2, T3> f2)
        {
            return async x => f2(await f1(x));
        }
    }
    
}