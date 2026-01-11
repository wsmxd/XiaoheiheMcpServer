# 小黑盒发布指南

本文档说明如何使用 MCP 服务器的各种发布功能。

## 三种发布方式

小黑盒创作者中心提供三种不同的发布方式，每种都有对应的 MCP 工具：

### 1. 发布图文 (publish_content)

**URL**: `https://www.xiaoheihe.cn/creator/editor/draft/image_text`

**适用场景**: 
- 简短的图文内容
- 以图片为主的内容
- 快速分享

**工具**: `publish_content`

**参数**:
- `title`: 标题（必需）
- `content`: 正文内容（必需）
- `images`: 图片路径数组（可选，本地绝对路径）
- `tags`: 标签数组（可选）

**使用示例**:
```
帮我在小黑盒发布一条图文内容
标题：今天的游戏体验
内容：今天体验了新游戏，感觉很不错！
图片：D:\Pictures\game1.jpg
标签：游戏,体验分享
```

**特点**:
- 底部有简单的工具栏（表情、@、游戏、话题、图片等）
- 适合快速发布
- 图片上传通过工具栏中的图片图标

---

### 2. 发布文章 (publish_article)

**URL**: `https://www.xiaoheihe.cn/creator/editor/draft/article`

**适用场景**:
- 长篇文章
- 详细的游戏评测、攻略
- 需要丰富格式的内容

**工具**: `publish_article`

**参数**:
- `title`: 文章标题（必需）
- `content`: 文章正文（必需）
- `images`: 图片路径数组（可选）
- `tags`: 标签数组（可选）

**使用示例**:
```
帮我在小黑盒发布一篇文章
标题：《原神》新版本深度评测
内容：这次的新版本带来了很多新内容...（长文）
图片：D:\Pictures\review1.jpg, D:\Pictures\review2.jpg
标签：原神,评测,攻略
```

**特点**:
- 富文本编辑器，支持更多格式选项
- 工具栏功能更丰富（标题、粗体、引用、列表等）
- 适合长文章创作
- 支持在文章中间插入图片

---

### 3. 发布视频 (publish_video)

**URL**: `https://www.xiaoheihe.cn/creator/editor/draft/video`

**适用场景**:
- 游戏录像
- 视频教程
- 实况分享

**工具**: `publish_video`

**参数**:
- `title`: 视频标题（必需）
- `description`: 视频描述（必需）
- `videoPath`: 视频文件路径（必需，本地绝对路径）
- `coverImagePath`: 封面图路径（可选）
- `tags`: 标签数组（可选）

**使用示例**:
```
帮我在小黑盒发布一个视频
标题：原神新角色演示
描述：展示新角色的技能和玩法
视频：D:\Videos\genshin_demo.mp4
封面：D:\Pictures\cover.jpg
标签：原神,角色演示
```

**特点**:
- 点击上传视频区域
- 支持设置封面图
- 上传时间较长（取决于视频大小）
- 自动等待视频处理完成

---

## 图片上传机制

### 实现逻辑

所有三种发布方式都支持图片上传，采用以下策略：

1. **优先查找已存在的文件输入框**
   ```csharp
   input[type='file'][accept*='image']
   input[type='file']
   ```

2. **如果未找到，点击工具栏中的图片按钮**
   - 查找工具栏中的 SVG 图标
   - 查找带有 `title="图片"` 属性的元素
   - 查找包含图片图标的按钮

3. **定位并点击可点击元素**
   ```javascript
   // 找到 SVG 图标的父级按钮
   element.closest('button, div[role="button"], a')
   ```

4. **上传文件并等待**
   - 使用 Playwright 的 `SetInputFilesAsync()` 方法
   - 每张图片等待 3 秒 + 基础 2 秒
   - 记录日志以便调试

### 选择器优先级

```javascript
// 1. 专用图片文件输入
"input[type='file'][accept*='image']"

// 2. 通用文件输入
"input[type='file']"

// 3. 工具栏中的图标
"[class*='toolbar'] svg"
"[class*='editor-toolbar'] svg"

// 4. 带标题属性
"[title*='图片']"
"[aria-label*='图片']"

// 5. 按钮中的图标
"button svg"
```

---

## 故障排查

### 图片上传失败

**症状**: 日志显示 "未找到文件上传控件"

**解决方案**:
1. 使用有界面模式运行查看实际页面：
   ```bash
   dotnet run -- --no-headless
   ```

2. 检查日志，查看尝试了哪些选择器

3. 在浏览器开发者工具中手动查找正确的选择器

4. 更新 `XiaoheiheService.cs` 中的选择器数组

### 视频上传超时

**症状**: 视频上传时间过长或超时

**解决方案**:
1. 确保视频文件不要太大（建议 < 500MB）
2. 检查网络连接
3. 增加 `PublishVideoAsync` 中的等待时间
4. 使用有界面模式观察上传进度

### 文本内容未填充

**症状**: 标题或内容没有填写到编辑器中

**解决方案**:
1. 检查页面是否使用 `contenteditable` 编辑器
2. 尝试使用键盘输入而不是直接填充
3. 增加操作之间的延迟时间

---

## 调试技巧

### 1. 启用详细日志

服务中已包含详细的日志输出：
- `LogInformation`: 正常流程
- `LogWarning`: 警告信息
- `LogError`: 错误信息
- `LogDebug`: 调试信息

### 2. 使用有界面模式

```bash
cd XiaoheiheMcpServer
dotnet run -- --no-headless
```

可以看到浏览器实际操作过程，便于定位问题。

### 3. 截图调试

在关键步骤添加截图：
```csharp
await _page.ScreenshotAsync(new() { Path = "debug.png" });
```

### 4. 检查元素

在操作失败时，记录页面 HTML：
```csharp
var html = await _page.ContentAsync();
_logger.LogDebug($"Page HTML: {html}");
```

---

## 最佳实践

1. **图片格式**: 使用 JPG 或 PNG 格式
2. **图片大小**: 建议每张 < 5MB
3. **视频格式**: MP4 格式兼容性最好
4. **标签使用**: 2-5 个相关标签效果最佳
5. **内容长度**: 
   - 图文：500-2000 字
   - 文章：1000-5000 字
   - 视频描述：100-500 字

---

## 技术细节

### Playwright 文件上传

```csharp
// 单个文件
await fileInput.SetInputFilesAsync("path/to/file.jpg");

// 多个文件
await fileInput.SetInputFilesAsync(new[] { 
    "path/to/file1.jpg", 
    "path/to/file2.jpg" 
});
```

### 动态查找按钮

```csharp
// 查找 SVG 图标的父级可点击元素
var parent = await element.EvaluateHandleAsync(
    "el => el.closest('button, div[role=\"button\"], a')"
);
```

### 等待策略

```csharp
// 等待网络空闲
await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

// 等待选择器出现
await _page.WaitForSelectorAsync(selector, new() { 
    Timeout = 10000 
});

// 固定延迟
await Task.Delay(2000);
```
