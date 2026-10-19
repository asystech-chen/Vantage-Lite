# 架构与模块地图

## 设计原则

1. 一个类一个文件，单文件目标 ≤ 200 行；大的类按职责拆 partial
2. UI 不碰文件 IO，服务层不碰控件
3. 每个文件顶部写：职责 / 依赖 / 改动注意
4. 主窗口只做装配，布局、导航、快捷键、状态、事件分别拆文件
5. 新功能先加模块，不往 `MainForm` 里堆

## 文件地图

| 文件 | 职责 | 行数 |
|---|---|---|
| `src/Program.cs` | 入口、单文件引导、日志初始化 | 130 |
| `src/App/EmbeddedAssemblies.cs` | 嵌入 DLL 解析、Loader 解压 | 110 |
| `src/App/CommandLineOptions.cs` | 命令行解析 | 155 |
| `src/App/AppPaths.cs` | 默认路径、便携模式、日志路径 | 88 |
| `src/App/ConsoleHelper.cs` | WinExe 附加控制台 | 41 |
| `src/App/CrashGuard.cs` | 全局异常兜底 | 61 |
| `src/Diagnostics/LogLevel.cs` | 日志级别枚举 | 14 |
| `src/Diagnostics/Log.cs` | 线程安全日志 | 160 |
| `src/Diagnostics/DiagnosticsInfo.cs` | OS / CLR / WebView2 版本 / 进程树内存 | 120 |
| `src/Theming/ThemePalette.cs` | 明暗两套配色 | 80 |
| `src/Theming/ThemeManager.cs` | 系统主题检测、切换事件、DWM 标题栏 | 140 |
| `src/Theming/IconFont.cs` | Fluent / MDL2 图标字体与码位 | 70 |
| `src/Theming/ThemeFonts.cs` | 系统 UI 字体 | 20 |
| `src/Browser/UrlHelper.cs` | 输入规范化、搜索回退 | 55 |
| `src/Browser/WebViewEnvironment.cs` | 环境创建、Runtime 检测 | 60 |
| `src/Browser/WebView2Bootstrap.cs` | 单文件 Loader 路径 | 30 |
| `src/Browser/DirtyFormScript.cs` | 脏表单标记脚本 | 20 |
| `src/Browser/BrowserTab.cs` | 单标签状态与导航 API | 191 |
| `src/Browser/BrowserTab.Events.cs` | WebView2 事件处理 | 121 |
| `src/Browser/BrowserTab.Lifecycle.cs` | 挂起/恢复/丢弃/滚动保存 | 170 |
| `src/Browser/TabManager.cs` | 标签创建/切换/关闭 | 225 |
| `src/Browser/TabManager.Internal.cs` | 查找、日志、事件转发 | 100 |
| `src/Browser/TabManager.Maintenance.cs` | 挂起、硬上限销毁、脏表单跳过 | 130 |
| `src/UI/MainForm.cs` | 装配、环境初始化、生命周期 | 100 |
| `src/UI/MainForm.Theme.cs` | 主题初始化与切换 | 60 |
| `src/UI/MainForm.Layout.cs` | 控件创建与布局 | 67 |
| `src/UI/MainForm.Events.cs` | 标签事件转发与活动标签绑定 | 170 |
| `src/UI/MainForm.Navigation.cs` | 导航与标签操作 | 102 |
| `src/UI/MainForm.Shortcuts.cs` | 快捷键 | 106 |
| `src/UI/MainForm.State.cs` | 活动标签状态同步 | 44 |
| `src/UI/NavigationBar.cs` | 导航条控件 | 220 |
| `src/UI/TabStrip.cs` | 标签条容器 | 200 |
| `src/UI/TabViewItem.cs` | 单个标签按钮 | 150 |

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
| 改标签条外观 | `TabViewItem.cs` |
| 改主题配色 | `ThemePalette.cs` |
| 改主题检测/切换 | `ThemeManager.cs` |
| 改图标 | `IconFont.cs` |
| 改标签条行为/排序 | `TabStrip.cs` |
| 改标签生命周期 | `TabManager.cs` |
| 改 LRU 丢弃策略 | `TabManager.Internal.cs` |
| 改单标签导航/事件 | `BrowserTab.cs` / `BrowserTab.Events.cs` |
| 改快捷键 | `MainForm.Shortcuts.cs` |
| 改导航动作 | `MainForm.Navigation.cs` |
| 改标题/地址栏同步 | `MainForm.State.cs` |

## 已实现里程碑

| 里程碑 | 内容 |
|---|---|
| M0 | 单标签、地址栏、导航、日志、诊断 |
| M1 | 多标签、标签条、LRU 丢弃、进程树内存统计 |
| UI-1 | 标签条置顶、明暗主题跟随系统、Fluent 图标、标签样式 |
| M2 | 后台挂起、脏表单保护、单文件 exe、便携数据目录、内存基线 |

## 后续模块（未创建）

| 里程碑 | 新增文件 |
|---|---|
| M2 书签/历史 | `Services/BookmarksStore.cs`、`Services/HistoryStore.cs` |
| M3 会话恢复 | `Services/SessionStore.cs` |
| M4 设置/更新 | `UI/SettingsForm.cs`、`Services/Updater.cs` |

## 标签生命周期设计

```
Active      可见，Normal
Background  隐藏，TrySuspendAsync()，保留 DOM/表单/滚动
Discarded   仅超硬上限或挂起超时，且表单不脏；保存 URL + 滚动位置
```

- 默认后台静默 30 秒后挂起
- `--max-live-tabs` 是 WebView2 实例硬上限（默认 10）
- 挂起超过 `--discard-after-minutes`（默认 60）才允许真正销毁
- 销毁前检查 `window.__vantageDirty`，有未提交表单则跳过
- 活动标签永远不挂起、不销毁
