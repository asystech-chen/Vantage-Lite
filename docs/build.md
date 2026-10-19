# 构建说明

## 环境

- Windows 10 1903+ / Windows 11（x64）
- 二选一：
  - .NET SDK 8 或更高（推荐，`dotnet build`）
  - Visual Studio 2026（使用自带 MSBuild）

`build.cmd` 会先找 `dotnet`，找不到再用 `vswhere` 定位 MSBuild。

## 构建

```bat
cd /d %USERPROFILE%\vantage-lite
build.cmd
```

成功输出：

```
=== Build OK ===
Output: C:\Users\<you>\vantage-lite\bin\Release\net48\
```

## 产物

```
bin\Release\net48\VantageLite.exe
```

**只有一个 exe。** 下列内容全部在构建时嵌入：

- `Microsoft.Web.WebView2.Core.dll`
- `Microsoft.Web.WebView2.WinForms.dll`
- `WebView2Loader.dll`（x64）

运行时：

- 托管 DLL 通过 `AssemblyResolve` 从 exe 资源加载
- 原生 Loader 解压到 `%TEMP%\VantageLite\Loader\x64-<pid>\`
- 用 `CoreWebView2Environment.SetLoaderDllFolderPath` 指定该目录

## 便携数据

```
<exe目录>\
├── VantageLite.exe
├── UserData\        WebView2 profile（Cookie、缓存、LocalStorage）
└── Logs\            日志
```

不写注册表、不写 `%LOCALAPPDATA%`。请把 exe 放在可写目录，不要放 `Program Files`。

## 常见问题

### 构建时报找不到 WebView2 程序集

先还原：

```bat
dotnet restore
```

项目用 `GeneratePathProperty` 从 NuGet 缓存取 DLL 路径。如果包缓存损坏，清掉 `%USERPROFILE%\.nuget\packages\microsoft.web.webview2` 后重新还原。

### 输出目录仍然有 DLL

确认 `VantageLite.csproj` 里的 `RemoveEmbeddedDependencies` target 没有被删掉。该 target 会在构建后清理残留依赖。

### 启动提示缺少 WebView2 Runtime

这是预期行为。Vantage Lite 会弹窗询问是否打开官方下载页：

https://developer.microsoft.com/microsoft-edge/webview2/

安装 Evergreen Runtime 后重新启动即可。

### 生成的 exe 是 32 位

确认 `PlatformTarget` 为 `x64`，且构建时没有被外部参数覆盖。

### 双击 exe 没反应

用命令行启动看错误：

```bat
VantageLite.exe --log --console --log-level=debug
```

如果连日志都没有，检查 exe 是否被杀软拦截，或 exe 目录是否可写。

## 升级 WebView2 SDK

```bat
dotnet add package Microsoft.Web.WebView2
```

csproj 里嵌入路径写死了 `net462` 和 `win-x64`；升级后如果包结构变化，需要同步修改 `EmbeddedResource` 段。
