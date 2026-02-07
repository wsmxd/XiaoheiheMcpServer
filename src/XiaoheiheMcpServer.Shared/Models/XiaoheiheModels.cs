namespace XiaoheiheMcpServer.Shared.Models;

using System.Text.Json.Serialization;

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
    public string QrDataPath { get; set; } = string.Empty;
    public DateTime ExpireTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 通用参数（社区和标签）
/// </summary>
public record CommonArgs(
    List<string> Communities,
    List<string> Tags
);

/// <summary>
/// 发布内容参数
/// </summary>
public class PublishContentArgs
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = [];
    
    [JsonPropertyName("communities")]
    public List<string> Communities { get; set; } = [];
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// 发布文章参数（长文章，与图文不同）
/// </summary>
public class PublishArticleArgs
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = [];
    
    [JsonPropertyName("communities")]
    public List<string> Communities { get; set; } = [];
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// 发布视频参数
/// </summary>
public class PublishVideoArgs
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("videoPath")]
    public string VideoPath { get; set; } = string.Empty;
    
    [JsonPropertyName("coverImagePath")]
    public string CoverImagePath { get; set; } = string.Empty;
    
    [JsonPropertyName("communities")]
    public List<string> Communities { get; set; } = [];
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// 评论参数
/// </summary>
public class CommentArgs
{
    [JsonPropertyName("postId")]
    public string PostId { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = [];
}

/// <summary>
/// 搜索参数
/// </summary>
public class SearchArgs
{
    [JsonPropertyName("keyword")]
    public string Keyword { get; set; } = string.Empty;
    
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;
    
    private int _pageSize = 20;
    
    /// <summary>每页大小，最多20条</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, 20); // 最多返回20条
    }
}

/// <summary>
/// 帖子详情参数
/// </summary>
public class PostDetailArgs
{
    [JsonPropertyName("postId")]
    public string PostId { get; set; } = string.Empty;
}

/// <summary>
/// 帖子详情（完整信息）
/// </summary>
public class PostDetail
{
    /// <summary>帖子ID</summary>
    public string PostId { get; set; } = string.Empty;
    
    /// <summary>标题</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>封面图片URL列表</summary>
    public List<string> CoverImages { get; set; } = [];
    
    /// <summary>正文内容（纯文本）</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>标签列表</summary>
    public List<string> Tags { get; set; } = [];
    
    /// <summary>评论总数</summary>
    public int CommentCount { get; set; } = 0;
    
    /// <summary>评论列表</summary>
    public List<CommentItem> Comments { get; set; } = [];
}

/// <summary>
/// 评论项
/// </summary>
public class CommentItem
{
    /// <summary>评论内容</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>点赞数</summary>
    public int LikeCount { get; set; } = 0;
}

/// <summary>
/// 搜索结果项
/// </summary>
public class SearchResultItem
{
    /// <summary>帖子ID</summary>
    public string PostId { get; set; } = string.Empty;
    
    /// <summary>标题</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>内容摘要（纯文本）</summary>
    public string ContentPreview { get; set; } = string.Empty;
    
    /// <summary>帖子链接</summary>
    public string Link { get; set; } = string.Empty;
    
    /// <summary>评论数</summary>
    public int CommentCount { get; set; } = 0;
    
    /// <summary>点赞数</summary>
    public int LikeCount { get; set; } = 0;
    
    /// <summary>图片列表</summary>
    public List<string> ImageUrls { get; set; } = [];
}