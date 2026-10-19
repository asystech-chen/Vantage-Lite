# 架构与模块地图

## 设计原则

1. 一个类一个文件，单文件目标 ≤ 200 行
2. UI 不碰文件 IO，服务层不碰控件
3. 每个文件顶部写：职责 / 依赖 / 改动注意
4. 主窗口只做装配，布局单独放 `MainForm.Layout.cs`
5. 新功能先加模块，不往 `MainForm` 里堆

## 文件地图

| 文件 | 职责 | 行数预算 |
|---|---|---|
| `src/Program.cs` | 入口、日志初始化、异常保护、启动窗口 | 100 |
| `src/App/CommandLineOptions.cs` | 命令行解析 | 150 |
| `src/App/AppPaths.cs` | 默认路径、便携模式、日志路径 | 100 |
| `src/App/ConsoleHelper.cs` | WinExe 附加控制台 | 50 |
| `src/App/CrashGuard.cs` | 全局异常兜底 | 80 |
| `src/Diagnostics/LogLevel.cs` | 日志级别枚举 | 20 |
| `src/Diagnostics/Log.cs` | 线程安全日志 | 180 |
| `src/Diagnostics/DiagnosticsInfo.cs` | OS / CLR / WebView2 版本 / 内存 | 100 |
| `src/Browser/UrlHelper.cs` | 输入规范化、搜索回退 | 80 |
| `src/Browser/WebViewEnvironment.cs` | 环境创建、Runtime 检测 | 100 |
| `src/UI/MainForm.cs` | 导航逻辑、快捷键、生命周期 | 180 |
| `src/UI/MainForm.Events.cs` | WebView2 事件处理 | 100 |
| `src/UI/MainForm.Layout.cs` | 控件创建与布局 | 100 |
| `src/UI/NavigationBar.cs` | 导航条控件 | 200 |

## 改哪里

| 需求 | 文件 |
|---|---|
| 新增命令行开关 | `CommandLineOptions.cs` + `Program.PrintHelp` |
| 改日志格式/落盘 | `Log.cs` |
| 日志里加环境信息 | `DiagnosticsInfo.cs` |
| 改默认首页/搜索 | `MainForm.DefaultHomeUrl` / `UrlHelper` |
| 改地址栏行为 | `UrlHelper.cs` |
| 改窗口布局/尺寸 | `MainForm.Layout.cs` |
| 改导航按钮/地址栏 UI | `NavigationBar.cs` |
| 改导航逻辑/快捷键 | `MainForm.cs` |
| WebView2 初始化失败 | `WebViewEnvironment.cs` |

## 后续模块（未创建）

| 里程碑 | 新增文件 |
|---|---|
| M1 多标签 | `Browser/BrowserTab.cs`、`Browser/TabManager.cs`、`UI/TabStrip.cs` |
| M2 书签/历史 | `Services/BookmarksStore.cs`、`Services/HistoryStore.cs` |
| M3 会话与丢弃 | `Services/SessionStore.cs`、`Browser/TabDiscarder.cs` |
| M4 设置/更新 | `UI/SettingsForm.cs`、`Services/Updater.cs` |
