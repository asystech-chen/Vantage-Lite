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
bin\Release\net48\
├── VantageLite.exe
├── VantageLite.exe.config
├── Microsoft.Web.WebView2.Core.dll
├── Microsoft.Web.WebView2.WinForms.dll
├── WebView2Loader.dll
└── runtimes\...
```

分发时打包整个目录。只拷 exe 会缺少 WebView2 托管 DLL 和 Loader。

## 常见问题

### NuGet 还原失败

检查网络和 NuGet 源：

```bat
dotnet nuget list source
```

需要能访问 nuget.org。项目固定了两个包：

- `Microsoft.Web.WebView2` 1.0.4258.31
- `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3（用于在没有 VS 的机器上编译 net48）

### 提示找不到 System.Windows.Forms

项目使用 SDK 风格 + 显式引用。确认没有改动 csproj 中的 `<Reference>` 段。

### 启动提示缺少 WebView2 Runtime

安装 Evergreen Runtime：

https://developer.microsoft.com/microsoft-edge/webview2/

### 生成的 exe 是 32 位

确认 `PlatformTarget` 为 `x64`，且构建时没有被外部参数覆盖。

## 升级 WebView2 SDK

```bat
dotnet add package Microsoft.Web.WebView2
```

或修改 csproj 中的版本号后重新构建。
