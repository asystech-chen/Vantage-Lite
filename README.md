# Vantage Lite

Windows 极简浏览器外壳，基于 **WebView2 (Edge Chromium)**。

- 目标：启动即用、无账号、无同步、无推荐、无后台服务
- 非目标：扩展、密码管理、主题、与 Vantage (Firefox) 配置兼容
- 当前版本：M0（单标签骨架）

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

## 快捷键

| 快捷键 | 功能 |
|---|---|
| `Ctrl+L` | 聚焦地址栏 |
| `Ctrl+R` / `F5` | 刷新 |
| `Alt+←` / `Alt+→` | 后退 / 前进 |
| `Ctrl+W` | 关闭窗口 |
| `F12` | 开发者工具 |

## M0 测试清单

1. `build.cmd` 输出 `Build OK`
2. 双击 exe，能打开 ASYS 起始页
3. 地址栏输入 `asystech.cn`，回车能跳转
4. 地址栏输入 `测试 搜索`，回车走 Bing 搜索
5. `Alt+←`、`F5`、`Ctrl+L` 正常
6. `--log` 启动后，日志文件存在且包含启动环境信息
7. `--user-data-dir` 能生成独立 profile
8. `--help` 能显示开关说明

## 已知限制（M0）

- 单标签；新窗口请求会在当前视图打开
- 无书签、历史、下载管理、会话恢复
- 无自动更新
- 未签名，首次运行可能触发 SmartScreen

## 目录

```
src/App          命令行、路径、异常保护
src/Diagnostics  日志与环境信息
src/Browser      URL 规范化、WebView2 环境
src/UI           窗口、导航条
docs/            架构、构建、日志说明
```

详细模块地图见 `docs/architecture.md`。
