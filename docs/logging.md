# 日志说明

## 开关

| 开关 | 作用 |
|---|---|
| `--log` | 开启文件日志（默认关闭） |
| `--log-level=LEVEL` | `debug` / `info` / `warn` / `error` / `off`，默认 `info` |
| `--log-file=PATH` | 自定义日志文件；目录不存在会自动创建 |
| `--console` | 同时输出到控制台（会附加/创建控制台窗口） |

## 默认路径

```
%LOCALAPPDATA%\VantageLite\Logs\vantage-lite-YYYYMMDD-HHmmss.log
```

便携模式（exe 同目录存在 `portable.txt`）：

```
<exe目录>\data\Logs\vantage-lite-YYYYMMDD-HHmmss.log
```

## 格式

```
2026-10-19 22:30:01.123 [INFO ] Navigation starting: https://asystech.cn/start/index.html
2026-10-19 22:30:01.456 [WARN ] Navigation failed: ConnectionAborted
2026-10-19 22:30:02.001 [ERROR] WebView2 初始化失败
System.InvalidOperationException: ...
```

## 启动时自动记录

- 程序版本、完整命令行
- OS 版本、架构
- CLR 版本、进程架构
- exe 路径、当前工作集
- WebView2 Runtime 版本（或 not installed）
- User Data 目录
- 未识别参数（WARN）

## 运行中记录

- 每次导航的 URL
- 导航失败原因（`WebErrorStatus`）
- 新窗口请求
- WebView2 进程异常（`ProcessFailedKind`）
- 未处理异常与完整堆栈

## 注意

日志会记录访问过的 URL。对外发送日志前请自行检查是否包含敏感地址。

## 常用命令

```bat
:: 最详细
VantageLite.exe --log --log-level=debug --console

:: 指定文件，方便对比两次运行
VantageLite.exe --log --log-file=D:\tmp\run1.log

:: 干净 profile 复现问题
VantageLite.exe --log --console --user-data-dir=D:\tmp\vl-clean
```
