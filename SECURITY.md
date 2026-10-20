# 安全策略

## 支持范围

仅最新 Release 版本接受安全修复；开发中的提交不保证稳定。

## 报告漏洞

请通过 GitHub 的 **Security → Report a vulnerability** 私下报告，不要先开公开 Issue。

请尽量包含：

- 影响版本
- 复现步骤或 PoC
- 影响范围（数据泄露、任意代码执行、权限提升等）

这是个人维护项目，回复为尽力而为，不提供漏洞赏金。

## 分发说明

- 发布包目前**未做代码签名**，Windows SmartScreen 可能提示未知发布者
- 每个 Release 会附 `SHA256SUMS`，下载后请校验
- 只从本仓库 Releases 下载，不要使用第三方转载包
