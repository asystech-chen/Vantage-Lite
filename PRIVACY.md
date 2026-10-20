# 隐私说明

Vantage Lite 是本地运行的浏览器外壳。

## 不收集

- 无账号、无同步、无遥测、无统计上报
- 不上传书签、历史、设置或浏览数据
- 不包含广告、推荐或后台分析服务

## 本地数据

均保存在程序目录下（便携模式）或用户指定目录：

| 路径 | 内容 |
|---|---|
| `data/bookmarks.json` | 书签 |
| `data/history.json` | 浏览历史 |
| `data/settings.json` | 设置 |
| `UserData/` | WebView2 配置、缓存、Cookie 等 |
| `Logs/` | 诊断日志（仅 `--log` 时生成） |

删除对应文件或目录即可清除数据。

## 网络访问

- 浏览网页时按你的操作访问目标站点
- 默认启动页为 `https://asystech.cn/start/index.html`
- 为显示书签图标，程序会向书签所在站点请求 favicon（`/favicon.ico` 或页面声明的图标）
- WebView2 运行时自身的更新与网络行为由 Microsoft 管理，不在本项目控制范围内

## 联系

如对隐私有疑问，请在仓库 Issues 中提出。
