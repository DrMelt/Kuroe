# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的最小智能体骨架：终端 REPL、流式输出、自动工具调用。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 项目结构

- `Kuroe`：智能体会话、模型目录与配置的库，不涉及终端。作为库使用时由宿主提供 `appsettings.json` 并注册日志。
- `Kuroe.Cli`：终端入口，承载 REPL、斜杠命令与日志输出。

运行：

```powershell
dotnet run --project Kuroe.Cli
```

## 依赖准备

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的三个包：

```powershell
$ver = '0.1.2'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

升级 ApiHub 时同步改动 `$ver` 与 `Directory.Packages.props` 中的包版本。
