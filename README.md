# Vantage Lite

Windows 极简浏览器外壳，基于 **WebView2 (Edge Chromium)**。

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](LICENSE)

- 目标：启动即用、无账号、无同步、无推荐、无后台服务
- 非目标：扩展、密码管理、与 Vantage (Firefox) 配置兼容
- 当前版本：0.16.1
- 已实现：多标签、后台挂起/丢弃、书签与历史、书签栏拖拽、下载管理、会话恢复、单文件 exe、明暗主题、自绘标题栏（标签栏并入）、高 DPI 适配、内置页

## 构建

要求：Windows 10 1903+ / Windows 11，已安装 [.NET SDK](https://dotnet.microsoft.com/download)（或 VS2026 的 MSBuild）。

```bat
build.cmd
```

产物：

```
bin\Release\net48\VantageLite.exe
```

WebView2 的托管 DLL 和 x64 Loader 已嵌入 exe，**分发只需要这一个文件**。
运行时会在 exe 同目录创建 `UserData\` 和 `Logs\`，请放在可写目录。

## 运行

```bat
bin\Release\net48\VantageLite.exe
```

默认打开 ASYS 起始页：`https://asystech.cn/start/index.html`

运行前提：系统已安装 **WebView2 Evergreen Runtime**（Win11 和绝大多数 Win10 自带）。

## 命令行开关

| 开关 | 说明 |
|---|---|
| `--log` | 启用文件日志 |
| `--log-level=LEVEL` | `debug` / `info` / `warn` / `error` / `off`，默认 `info` |
| `--log-file=PATH` | 指定日志文件 |
| `--console` | 日志同时输出到控制台 |
| `--user-data-dir=PATH` | 指定用户数据目录（独立 profile） |
| `--max-live-tabs=N` | 最多保留的 WebView2 实例数，默认 8（1-50） |
| `--suspend-after-seconds=N` | 后台标签静默多少秒后挂起，默认 30（0=不挂起），会覆盖设置页 |
| `--discard-after-minutes=N` | 挂起多少分钟后销毁，默认 60（0=不销毁），会覆盖设置页 |
| `--url=URL` | 指定启动页，也可直接传 URL |
| `--no-devtools` | 禁用 F12 开发者工具 |
| `--version` | 显示版本 |
| `--help` | 显示帮助 |

示例：

```bat
VantageLite.exe --log --console
VantageLite.exe --log --log-level=debug --log-file=D:\tmp\vl.log
VantageLite.exe --user-data-dir=D:\tmp\vl-profile --url=https://asystech.cn
```

默认日志位置：

```
<exe目录>\Logs\vantage-lite-YYYYMMDD-HHmmss.log
```

用户数据：`<exe目录>\UserData\`。所有文件都跟着 exe 走，不写 `%LOCALAPPDATA%`。

## 外观

- 自绘标题栏（36px），标签条与标题栏合并；右侧为最小化/最大化/关闭
- 无边框窗口：四角/左右/下边缩放，顶边与标签条空白处拖动窗口，双击最大化
- 窗口位置、尺寸、最大化状态保存在 `data/window.json`，下次启动恢复
- 跟随 Windows 明暗主题，切换时实时更新
- 所有界面尺寸与图标按 DPI 缩放；图标字形固定像素，避免高缩放下过大
- 网页内容同步 `prefers-color-scheme`
- 图标使用 Segoe Fluent Icons / Segoe MDL2 Assets
- 正文使用 Windows 系统 UI 字体（SystemFonts.MessageBoxFont）

## 快捷键

| 快捷键 | 功能 |
|---|---|
| `Ctrl+L` | 聚焦地址栏 |
| `Ctrl+R` / `F5` | 刷新 |
| `Alt+←` / `Alt+→` | 后退 / 前进 |
| `Ctrl+T` | 新建标签页 |
| `Ctrl+W` | 关闭当前标签页 |
| `Ctrl+J` | 下载面板 |
| `Ctrl+D` | 收藏当前页面 |
| `Ctrl+H` | 历史记录 |
| `Ctrl+Shift+B` | 显示/隐藏书签栏 |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | 下一个 / 上一个标签页 |
| `Ctrl+1` ~ `Ctrl+8` | 切换到第 1~8 个标签 |
| `Ctrl+9` | 切换到最后一个标签 |
| `F12` | 开发者工具 |

## 功能

- 多标签：新建、切换、中键关闭、拖动排序；标签条溢出时横向滚动
- 内存：后台 30 秒挂起、挂起被拒降级、存活标签上限 + LRU 丢弃、最小化节流
- 书签：树形文件夹、书签工具栏、`about:bookmarks` 管理页、拖拽排序/移入文件夹、搜索
- 历史：`about:history`，即时落盘，最多 2000 条；时间范围 + 关键词过滤、一键清空
- 下载：弹出式下载面板（`Ctrl+J`）+ `about:downloads`，暂停/继续/取消/重试/打开文件/目录，历史持久化
- 会话恢复：启动时恢复上次标签（与「关闭时清空」互斥）
- 新标签页：`about:newtab`（logo + wordmark + 搜索框，可选书签快捷入口），可切换内置页/空白页/当前主页/自定义网址
- 设置：`about:preferences`（启动页、新标签页、主题、最大存活标签、挂起/销毁等待、搜索引擎、书签栏、下载目录、隐私清理）
- 关于：自绘对话框，logo 来自 `Resources/vantage-logo.svg`，跟随 Windows 文本大小
- 外观：自绘标题栏、跟随系统明暗主题、内置页与原生菜单同步切换
- 图标：页面 favicon、书签栏图标、Windows 系统文件夹图标（`SHGetFileInfo`）
- 快捷键：见上方表格

## 路线图

短期（下一批）：

- 标签条右键菜单（关闭其他/右侧、重开、复制）与标签拖拽排序
- 书签导入/导出（HTML + `.url`；导入需另找验证环境）
- 书签 URL 编辑
- 历史按日期分组（今天/昨天/更早）
- 下载项增强：复制链接、打开来源页、删除磁盘文件、完成提示

中期：

- 会话恢复增强：崩溃/强杀定时快照、多窗口
- 小尺寸 logo 简化版（16/20/24）
- `about:about` 内置页内联 SVG logo
- 评估更可靠的 Mica/背景材质方案（当前 `enableMica` 为实验项）

## 已知限制

- 无扩展、无自动更新、无密码管理
- 未签名，首次运行可能触发 SmartScreen
- 脏表单检测只覆盖顶层文档的 `input/change`，跨域 iframe 和复杂 SPA 可能漏判
- “轻量”指安装包体积、零运行时安装和启动速度；WebView2 的内存占用与 Edge 同量级

## 目录

```
src/App          命令行、路径、异常保护、单文件装配
src/Diagnostics  日志、环境信息、内存基线
src/Browser      URL、WebView2 环境、标签生命周期
src/Downloads    下载记录与管理
src/Storage      JSON 存储（书签/历史/设置/窗口状态/会话）
src/Theming      明暗配色、主题检测、图标字体
src/UI           窗口、标题栏、导航条、标签条、内置页
Resources        图标与 logo（ico/svg/png）
docs/            架构、构建、日志说明
```

详细模块地图见 `docs/architecture.md`。

## 许可

本项目代码采用 **Mozilla Public License 2.0**，全文见 [LICENSE](LICENSE)。

- 第三方组件与商标说明：[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- 隐私说明：[PRIVACY.md](PRIVACY.md)
- 安全策略：[SECURITY.md](SECURITY.md)
- 参与贡献：[CONTRIBUTING.md](CONTRIBUTING.md)
- 更新日志：[CHANGELOG.md](CHANGELOG.md)

Vantage Lite 与 Mozilla、Firefox、Microsoft、Edge 均无隶属或背书关系。
