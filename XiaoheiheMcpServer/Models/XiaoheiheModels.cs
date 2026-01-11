namespace XiaoheiheMcpServer.Models;

/// <summary>
/// 登录状态
/// </summary>
public class LoginStatus
{
    public bool IsLoggedIn { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 二维码登录信息
/// </summary>
public class QrCodeInfo
{
    public string QrCodeBase64 { get; set; } = string.Empty;
    public DateTime ExpireTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 发布内容参数
/// </summary>
public class PublishContentArgs
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 发布文章参数（长文章，与图文不同）
/// </summary>
public class PublishArticleArgs
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 发布视频参数
/// </summary>
public class PublishVideoArgs
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VideoPath { get; set; } = string.Empty;
    public string? CoverImagePath { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 评论参数
/// </summary>
public class CommentArgs
{
    public string PostId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 搜索参数
/// </summary>
public class SearchArgs
{
    public string Keyword { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 帖子详情参数
/// </summary>
public class PostDetailArgs
{
    public string PostId { get; set; } = string.Empty;
}
