namespace XiaoheiheMcpServer.Models;

/// <summary>
/// MCP工具调用结果
/// </summary>
public class McpToolResult
{
    public List<McpContent> Content { get; set; } = [];
    public bool IsError { get; set; }
}

/// <summary>
/// MCP内容
/// </summary>
public class McpContent
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
    public string? Data { get; set; }
    public string? MimeType { get; set; }
}
