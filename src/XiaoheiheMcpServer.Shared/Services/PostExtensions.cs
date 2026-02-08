namespace XiaoheiheMcpServer.Shared.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using XiaoheiheMcpServer.Shared.Models;

public static class PostExtensions
{
    
    extension(IPage page)
    {
        /// <summary>
        /// 拿到回复评论的元素
        /// </summary>
        public async Task ClickReplyCommentAsync(string content, ILogger logger)
        {
            var commentElements = await page.QuerySelectorAllAsync(".comment-item__content");
            logger.LogInformation($"找到 {commentElements.Count} 条评论元素，准备查找内容为 '{content}' 的评论");
            foreach (var commentElement in commentElements)
            {
                var textContent = await commentElement.TextContentAsync();
                if (textContent != null && textContent.Trim().Contains(content.Trim()))
                {
                    await commentElement.ClickAsync(new ElementHandleClickOptions
                    {
                        Button = MouseButton.Right
                    });
                    await Task.Delay(800); // 等待右键菜单出现
                    
                    // 获取右键菜单中的回复按钮
                    var replyButton = page.Locator("div.hb-cpt__popover-menu-item.plain", new PageLocatorOptions
                    {
                        HasTextString = "回复评论"
                    });
                    if (await replyButton.CountAsync() == 0)
                    {
                        logger.LogInformation("没有找到评论的回复按钮");
                    }
                    else
                    {
                        await replyButton.First.ClickAsync();
                    }
                }
            }
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