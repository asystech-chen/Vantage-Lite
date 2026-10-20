# 贡献指南

感谢参与。请先提 Issue 说明问题或需求，达成一致后再提 PR，避免大改被拒。

## 开发环境

- Windows 10 1903+ / Windows 11
- .NET SDK（或 Visual Studio 2026 的 MSBuild）
- WebView2 Runtime

构建：

```bat
build.cmd
```

产物：`bin\Release\net48\VantageLite.exe`

## 代码约定

- C# 7.3 / .NET Framework 4.8，x64
- 一个类一个文件，单文件目标 ≤ 200 行；大类按职责拆 partial
- 界面颜色一律取自 `ThemeManager` / `ThemePalette`，不要硬编码
- 新增界面逻辑要同时适配明暗主题
- UI 不碰 IO，存储层不碰控件

## 提交

- 提交信息用中文，前缀 `feat:` / `fix:` / `docs:` / `chore:`
- 一个提交只做一件事
- PR 请说明：改了什么、为什么、如何验证

## 许可

提交的代码按 **MPL-2.0** 授权（inbound = outbound），无需签署额外 CLA。
