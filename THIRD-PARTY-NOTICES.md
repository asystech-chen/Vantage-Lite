# 第三方组件与许可

本项目自身代码采用 Mozilla Public License 2.0（见 `LICENSE`）。
以下组件按各自许可使用，其许可不因本项目的 MPL-2.0 而改变。

## 运行与构建依赖

| 组件 | 用途 | 许可 / 说明 |
|---|---|---|
| Microsoft Edge WebView2 SDK（`Microsoft.Web.WebView2`） | 浏览器内核宿主 | Microsoft 软件许可条款；NuGet 包及其运行库归 Microsoft 所有 |
| Microsoft Edge WebView2 Runtime（Evergreen） | 运行时内核 | 由系统或用户安装，本项目不打包、不再分发 |
| .NET Framework 4.8 / Reference Assemblies | 目标框架与编译 | Microsoft .NET Framework 许可条款；仅构建期使用 |

## 图标与素材

- `Resources/vantage.ico` 及程序图标：由本项目维护者提供，随本项目一并按 MPL-2.0 授权。
- 文件夹图标：运行时通过 Windows Shell（`SHGetFileInfo`）获取，不随仓库分发。

## 说明

- 本项目与 Mozilla、Firefox、Microsoft、Edge 均无隶属或背书关系。
- WebView2 是 Microsoft 的商标；相关名称仅用于说明技术依赖。
