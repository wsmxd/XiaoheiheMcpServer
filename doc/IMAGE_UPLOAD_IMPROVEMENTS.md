# 图片上传功能改进说明

## 改进内容

根据小黑盒创作者中心的实际页面结构，改进了图片上传逻辑以正确实现插入图片功能。

## 主要变化

### 1. 优化了图片上传流程

**之前**: 简单查找 `input[type='file']` 元素

**现在**: 
1. **第一步**: 优先查找已存在的文件输入框
2. **第二步**: 如果未找到，智能点击工具栏中的图片按钮
3. **第三步**: 点击后重新查找文件输入框
4. **第四步**: 上传文件并等待完成

### 2. 新增选择器策略

```javascript
// 文件输入框
"input[type='file'][accept*='image']"  // 专用图片输入
"input[type='file']"                    // 通用文件输入

// 工具栏按钮（从截图分析）
"[class*='toolbar'] svg"                // 工具栏中的SVG图标
"[class*='editor-toolbar'] svg"         
"[class*='bottom'] svg"                 // 底部SVG图标
"[title*='图片']"                       // 带提示的元素
"[aria-label*='图片']"                  
"button svg"                            // 按钮中的SVG
```

### 3. 智能按钮定位

使用 JavaScript 查找 SVG 图标的父级可点击元素：
```csharp
var parent = await element.EvaluateHandleAsync(
    "el => el.closest('button, div[role=\"button\"], a')"
);
```

### 4. 增强的日志记录

- 记录找到的每个选择器
- 记录上传进度
- 记录失败原因
- 提供调试建议

## 支持的三种发布方式

### 1. 发布图文 (publish_content)
- URL: `/creator/editor/draft/image_text`
- 适合：简短图文内容
- 工具栏：表情、@、游戏、话题、**图片**等

### 2. 发布文章 (publish_article) ✨ 新增
- URL: `/creator/editor/draft/article`  
- 适合：长篇文章、评测、攻略
- 工具栏：更丰富的编辑功能（标题、粗体、列表等）
- 富文本编辑器

### 3. 发布视频 (publish_video)
- URL: `/creator/editor/draft/video`
- 适合：视频内容
- 支持：封面图上传

## 文件清单

### 修改的文件

1. **XiaoheiheService.cs**
   - 改进 `PublishContentAsync()` 的图片上传逻辑
   - 新增 `PublishArticleAsync()` 方法
   - 优化 `PublishVideoAsync()` 的文件查找

2. **XiaoheiheModels.cs**
   - 新增 `PublishArticleArgs` 模型

3. **Program.cs**
   - 新增 `publish_article` MCP 工具

### 新增的文件

1. **PUBLISHING_GUIDE.md**
   - 详细的发布指南
   - 三种发布方式对比
   - 图片上传机制说明
   - 故障排查和调试技巧

## 使用示例

### 发布带图片的图文内容

```
帮我在小黑盒发布图文
标题：今日游戏分享
内容：今天玩了新游戏，体验很棒！
图片：D:\Pictures\game1.jpg, D:\Pictures\game2.jpg
标签：游戏,分享
```

### 发布带图片的文章

```
帮我发布一篇小黑盒文章
标题：原神3.5版本深度评测
内容：本次更新带来了许多新内容...（长文）
图片：D:\Reviews\pic1.jpg
标签：原神,评测
```

### 发布视频

```
发布视频到小黑盒
标题：新角色技能演示
描述：展示新角色的完整技能组合
视频：D:\Videos\demo.mp4
封面：D:\Pictures\cover.jpg
标签：演示,攻略
```

## 技术亮点

### 1. 渐进式查找策略
从简单到复杂，逐步查找正确的上传入口

### 2. DOM 遍历
使用 `closest()` JavaScript 方法找到可点击的父元素

### 3. 多选择器支持
针对不同页面结构提供多种选择器备选

### 4. 完善的错误处理
每个步骤都有 try-catch，提供清晰的错误信息

### 5. 详细的日志
便于调试和定位问题

## 调试建议

如果图片上传仍然失败：

1. **使用有界面模式运行**
   ```bash
   dotnet run -- --no-headless
   ```

2. **查看日志输出**
   确认尝试了哪些选择器

3. **手动检查页面**
   使用浏览器开发者工具查看实际的 HTML 结构

4. **更新选择器**
   根据实际页面结构更新选择器数组

5. **增加延迟**
   如果页面加载较慢，适当增加 `Task.Delay()` 时间

## 下一步

建议根据实际使用情况进一步优化：

1. 添加重试机制
2. 支持更多图片格式检测
3. 添加图片压缩功能
4. 支持拖拽上传的页面
5. 添加上传进度监控

## 注意事项

⚠️ **重要**: 
- 当前实现基于页面结构分析，实际效果需要测试验证
- 如果小黑盒更新页面结构，可能需要更新选择器
- 建议首次使用时用有界面模式观察实际效果
- Playwright 浏览器需要先安装（见 README.md）
