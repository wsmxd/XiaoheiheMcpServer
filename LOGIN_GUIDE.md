# 小黑盒 MCP 服务器 - 登录指南

## 📋 登录方案说明

我们提供了**两种登录方案**，推荐使用**方案1（交互式登录）**以获得最佳体验。

---

## ✅ 方案1：交互式登录（推荐）

### 特点
- ✨ **简单可靠**：打开浏览器，用你习惯的方式登录（验证码、密码、扫码都行）
- 🔒 **长期有效**：登录一次，Cookie 自动保存，后续无需重复登录
- 🎯 **稳定性高**：不依赖页面结构，不受网站改版影响

### 使用步骤

#### 1. 首次登录
调用 `interactive_login` 工具：

```json
{
  "name": "interactive_login",
  "arguments": {
    "waitTimeoutSeconds": 300
  }
}
```

#### 2. 在弹出的浏览器中登录
- 浏览器会自动打开小黑盒登录页面
- 你可以选择任意登录方式：
  - 手机号 + 验证码
  - 账号密码
  - 扫描二维码
- 登录成功后，Cookie 会自动保存

#### 3. 后续使用
登录后，所有工具都会自动使用保存的 Cookie，无需再次登录：
- `check_login_status` - 检查登录状态
- `publish_content` - 发布图文
- `publish_article` - 发布文章
- `publish_video` - 发布视频
- `search_content` - 搜索内容
- `post_comment` - 发表评论

### 超时设置
- 默认等待时间：300 秒（5分钟）
- 可自定义：`waitTimeoutSeconds` 参数

---

## 🔄 方案2：二维码登录（备用）

### 特点
- 📱 自动获取二维码
- 🤖 适合自动化场景
- ⚠️ 依赖页面结构，可能需要维护

### 使用步骤

#### 1. 获取二维码
调用 `get_login_qrcode` 工具：

```json
{
  "name": "get_login_qrcode",
  "arguments": {}
}
```

#### 2. 扫描二维码
- 工具会返回二维码图片（Base64格式）
- 使用小黑盒 APP 扫描二维码
- 等待登录成功，Cookie 自动保存

---

## 🔍 检查登录状态

随时使用 `check_login_status` 工具查看当前登录状态：

```json
{
  "name": "check_login_status",
  "arguments": {}
}
```

**返回示例**：
- ✅ 已登录：
  ```
  ✅ 已登录
  用户名: YourUsername
  
  你可以使用其他功能了。
  ```

- ❌ 未登录：
  ```
  ❌ 未登录
  
  未登录，请使用 interactive_login 工具进行首次登录
  
  推荐使用 interactive_login 工具进行首次登录（打开浏览器手动登录），
  或使用 get_login_qrcode 获取二维码。
  ```

---

## 🗂️ Cookie 管理

### Cookie 存储位置
```
XiaoheiheMcpServer/bin/Debug/net10.0/data/cookies.json
```

### Cookie 有效期
- 通常有效期较长（数周到数月）
- 过期后会自动提示重新登录

### 手动清除 Cookie
如需重新登录，删除 `cookies.json` 文件即可：

```powershell
Remove-Item "d:\vs\.NETWithAI\XiaoheiheMcpServer\XiaoheiheMcpServer\bin\Debug\net10.0\data\cookies.json"
```

---

## 🚀 完整使用流程示例

### 第一次使用

1. **检查登录状态**
   ```json
   {"name": "check_login_status"}
   ```
   返回：❌ 未登录

2. **交互式登录**
   ```json
   {"name": "interactive_login", "arguments": {"waitTimeoutSeconds": 300}}
   ```
   - 浏览器自动打开
   - 在浏览器中完成登录
   - 返回：✅ 登录成功！

3. **开始使用其他功能**
   ```json
   {"name": "publish_content", "arguments": {...}}
   ```

### 后续使用

直接使用任何功能，无需重新登录：

```json
{"name": "publish_content", "arguments": {...}}
{"name": "search_content", "arguments": {...}}
{"name": "post_comment", "arguments": {...}}
```

---

## ⚙️ 运行模式

### 无头模式（默认）
```powershell
dotnet run
```
- 后台运行，不显示浏览器窗口
- 适合生产环境

### 有头模式（调试）
```powershell
dotnet run -- --no-headless
```
- 显示浏览器窗口
- 适合调试、首次登录
- `interactive_login` 会自动使用有头模式

---

## 🛠️ 故障排除

### 问题1：交互式登录超时
**原因**：用户在规定时间内未完成登录

**解决**：
- 增加超时时间：`"waitTimeoutSeconds": 600`
- 或重新调用 `interactive_login`

### 问题2：Cookie 过期
**现象**：`check_login_status` 返回未登录

**解决**：
- 重新执行 `interactive_login` 登录

### 问题3：二维码获取失败
**原因**：页面结构变化

**解决**：
- 使用方案1（`interactive_login`）替代
- 或查看日志中的调试截图定位问题

---

## 📊 方案对比

| 特性 | 交互式登录（推荐） | 二维码登录 |
|------|-------------------|-----------|
| 易用性 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| 稳定性 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| 灵活性 | ⭐⭐⭐⭐⭐（支持所有登录方式） | ⭐⭐（仅扫码） |
| 维护成本 | ⭐⭐⭐⭐⭐（低） | ⭐⭐（需适配页面变化） |
| 自动化程度 | ⭐⭐⭐⭐（首次需手动） | ⭐⭐⭐⭐⭐（全自动） |

---

## 💡 最佳实践

1. **首次使用**：使用 `interactive_login` 进行登录
2. **日常使用**：直接调用功能工具，无需关心登录
3. **定期检查**：偶尔运行 `check_login_status` 确认状态
4. **Cookie 过期**：收到未登录提示时重新执行 `interactive_login`

---

**版本**: 2.0  
**最后更新**: 2026年1月11日
