using XiaoheiheMcpServer.Models;

namespace XiaoheiheMcpServer.Tests.Models;

/// <summary>
/// 模型测试
/// </summary>
public class ModelsTests
{
    [Fact]
    public void LoginStatus_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var status = new LoginStatus
        {
            IsLoggedIn = true,
            Username = "testuser",
            Message = "测试消息"
        };

        // Assert
        Assert.True(status.IsLoggedIn);
        Assert.Equal("testuser", status.Username);
        Assert.Equal("测试消息", status.Message);
    }

    [Fact]
    public void QrCodeInfo_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var qrInfo = new QrCodeInfo
        {
            QrCodeBase64 = "base64string",
            ExpireTime = DateTime.Now.AddMinutes(5),
            Message = "请扫码"
        };

        // Assert
        Assert.Equal("base64string", qrInfo.QrCodeBase64);
        Assert.True(qrInfo.ExpireTime > DateTime.Now);
        Assert.Equal("请扫码", qrInfo.Message);
    }

    [Fact]
    public void PublishContentArgs_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var args = new PublishContentArgs();

        // Assert
        Assert.Empty(args.Title);
        Assert.Empty(args.Content);
        Assert.Empty(args.Images);
        Assert.Empty(args.Tags);
    }

    [Fact]
    public void PublishContentArgs_ShouldAcceptValues()
    {
        // Arrange & Act
        var args = new PublishContentArgs
        {
            Title = "测试标题",
            Content = "测试内容",
            Images = new List<string> { "image1.jpg", "image2.jpg" },
            Tags = new List<string> { "游戏", "评测" }
        };

        // Assert
        Assert.Equal("测试标题", args.Title);
        Assert.Equal("测试内容", args.Content);
        Assert.Equal(2, args.Images.Count);
        Assert.Equal(2, args.Tags.Count);
    }

    [Fact]
    public void CommentArgs_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var args = new CommentArgs
        {
            PostId = "123",
            Content = "测试评论"
        };

        // Assert
        Assert.Equal("123", args.PostId);
        Assert.Equal("测试评论", args.Content);
    }

    [Fact]
    public void SearchArgs_ShouldHaveDefaultPageValues()
    {
        // Arrange & Act
        var args = new SearchArgs
        {
            Keyword = "搜索关键词"
        };

        // Assert
        Assert.Equal("搜索关键词", args.Keyword);
        Assert.Equal(1, args.Page);
        Assert.Equal(20, args.PageSize);
    }

    [Fact]
    public void McpToolResult_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var result = new McpToolResult();

        // Assert
        Assert.NotNull(result.Content);
        Assert.Empty(result.Content);
        Assert.False(result.IsError);
    }

    [Fact]
    public void McpContent_ShouldHaveTextTypeByDefault()
    {
        // Arrange & Act
        var content = new McpContent();

        // Assert
        Assert.Equal("text", content.Type);
        Assert.Empty(content.Text);
        Assert.Null(content.Data);
        Assert.Null(content.MimeType);
    }
}
