# Vantage Lite

Windows 极简浏览器外壳，基于 **WebView2 (Edge Chromium)**。

- 目标：启动即用、无账号、无同步、无推荐、无后台服务
- 非目标：扩展、密码管理、主题、与 Vantage (Firefox) 配置兼容
- 当前版本：0.3.1（UI-1 + 标签文字溢出修复）
- 已实现：多标签、标签条、后台标签丢弃、进程树内存统计、明暗主题跟随系统

## 构建

要求：Windows 10 1903+ / Windows 11，已安装 [.NET SDK](https://dotnet.microsoft.com/download)（或 VS2026 的 MSBuild）。

```bat
build.cmd
```

产物目录：

```
bin\Release\net48\
```

分发时请打包**整个目录**（包含 `WebView2Loader.dll` 和 `Microsoft.Web.WebView2.*.dll`），不要只拷 exe。

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
| `--max-live-tabs=N` | 同时存活的标签上限，默认 5（范围 1-50） |
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
%LOCALAPPDATA%\VantageLite\Logs\vantage-lite-YYYYMMDD-HHmmss.log
```

exe 同目录放一个 `portable.txt` 可切换为便携模式，数据写到 `.\data\`。

## 外观

- 标签条在最上方，地址栏在下方
- 跟随 Windows 明暗主题（`AppsUseLightTheme`），切换时实时更新
- 原生标题栏同步明暗（DWM）
- 网页内容同步 `prefers-color-scheme`
- 图标使用 Segoe Fluent Icons / Segoe MDL2 Assets

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

## M1 测试清单

1. `build.cmd` 输出 `Build OK`
2. 启动后能打开 ASYS 起始页
3. `Ctrl+T` 新建标签，标签条出现第二个标签
4. 在多个标签间切换，页面状态互不影响
5. `Ctrl+W` 关闭当前标签；关掉最后一个标签时窗口关闭
6. 页面里的 `target=_blank` 链接会在新标签打开
7. `Ctrl+Tab` / `Ctrl+Shift+Tab` 循环切换
8. 打开 6 个以上标签，日志出现 `discarded`，被丢弃标签标题前有 `◌`
9. 切回被丢弃的标签，能恢复原 URL 并重新加载
10. 日志里的 `[tabs]` 行包含 `total/live/active` 和 `host/webview2` 内存
11. 快速连续导航时，`ConnectionAborted` 只出现在 Debug 级别，不再是 WARN

## 已知限制（M1）

- 无会话恢复：关闭窗口后标签不保留
- 无书签、历史、下载管理
- 无自动更新
- 未签名，首次运行可能触发 SmartScreen
- 标签丢弃只按 LRU，不考虑页面是否有未提交表单

## 目录

```
src/App          命令行、路径、异常保护
src/Diagnostics  日志与环境信息
src/Browser      URL 规范化、WebView2 环境、标签管理
src/UI           窗口、导航条、标签条
docs/            架构、构建、日志说明
```

详细模块地图见 `docs/architecture.md`。
