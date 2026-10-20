# Vantage Lite

Windows 极简浏览器外壳，基于 **WebView2 (Edge Chromium)**。

- 目标：启动即用、无账号、无同步、无推荐、无后台服务
- 非目标：扩展、密码管理、与 Vantage (Firefox) 配置兼容
- 当前版本：0.7.2（右键菜单跟随指针 + 书签命令诊断日志）
- 已实现：多标签、后台挂起、脏表单保护、单文件 exe、明暗主题跟随系统

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
| `--max-live-tabs=N` | 最多保留的 WebView2 实例数，默认 10（1-50） |
| `--suspend-after-seconds=N` | 后台标签静默多少秒后挂起，默认 30（0=不挂起） |
| `--discard-after-minutes=N` | 挂起多少分钟后销毁，默认 60（0=不销毁） |
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

- 标签条在最上方，地址栏在下方
- 跟随 Windows 明暗主题（`AppsUseLightTheme`），切换时实时更新
- 原生标题栏同步明暗（DWM）
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
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | 下一个 / 上一个标签页 |
| `Ctrl+1` ~ `Ctrl+8` | 切换到第 1~8 个标签 |
| `Ctrl+9` | 切换到最后一个标签 |
| `F12` | 开发者工具 |

## M2 测试清单

1. `build.cmd` 输出 `Build OK`
2. `bin\Release\net48\` 里只有 `VantageLite.exe`（没有 DLL）
3. 双击 exe 能打开 ASYS 起始页
4. 新建标签并切换，功能正常
5. 填一个表单（不提交），切到别的标签等 30 秒以上
6. 切回原标签，表单内容仍在（说明是挂起不是销毁）
7. 日志出现 `suspend: ok`，`[tabs]` 行包含 `suspended=`
8. 把 `--suspend-after-seconds=0` 启动，后台标签不应挂起
9. 把 `--max-live-tabs=2` 开 3 个标签，超限时优先销毁无表单标签
10. 有未提交表单的标签不会被销毁（日志出现 `discard skipped`）
11. 日志里的 `ours=` 只统计本程序新增的 WebView2 进程
12. 退出后重开，确认 `UserData` 和 `Logs` 都在 exe 目录

## 已知限制（M2）

- 无会话恢复：关闭窗口后标签不保留
- 无书签、历史、下载管理
- 无自动更新
- 未签名，首次运行可能触发 SmartScreen
- 脏表单检测只覆盖顶层文档的 `input/change`，跨域 iframe 和复杂 SPA 可能漏判

## 目录

```
src/App          命令行、路径、异常保护、单文件装配
src/Diagnostics  日志、环境信息、内存基线
src/Browser      URL、WebView2 环境、标签生命周期
src/Theming      明暗配色、主题检测、图标字体
src/UI           窗口、导航条、标签条
docs/            架构、构建、日志说明
```

详细模块地图见 `docs/architecture.md`。
