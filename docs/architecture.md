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
| `src/Program.cs` | 入口、日志初始化、异常保护、启动窗口 | 97 |
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
| `src/Browser/UrlHelper.cs` | 输入规范化、搜索回退 | 55 |
| `src/Browser/WebViewEnvironment.cs` | 环境创建、Runtime 检测 | 56 |
| `src/Browser/BrowserTab.cs` | 单标签状态与导航 API | 191 |
| `src/Browser/BrowserTab.Events.cs` | WebView2 事件处理 | 121 |
| `src/Browser/BrowserTab.Lifecycle.cs` | 丢弃与销毁 | 60 |
| `src/Browser/TabManager.cs` | 标签创建/切换/关闭 | 196 |
| `src/Browser/TabManager.Internal.cs` | 查找、LRU 丢弃、日志、事件转发 | 110 |
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

## 后续模块（未创建）

| 里程碑 | 新增文件 |
|---|---|
| M2 书签/历史 | `Services/BookmarksStore.cs`、`Services/HistoryStore.cs` |
| M3 会话恢复 | `Services/SessionStore.cs` |
| M4 设置/更新 | `UI/SettingsForm.cs`、`Services/Updater.cs` |

## 标签丢弃设计

- 每个标签对应一个 `BrowserTab`，内部持有一个 WebView2
- 非活动标签隐藏控件，但保持 WebView2 存活
- 存活数量超过 `--max-live-tabs`（默认 5）时，按 `LastActivatedUtc` 丢弃最久未用的**非活动**标签
- 丢弃 = 释放 WebView2，保留 `BrowserTab`、URL、标题；标签条显示 `◌`
- 切回被丢弃标签时重建 WebView2，并重新导航到保存的 URL
- 当前活动标签永远不会被丢弃
