# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的最小智能体骨架：终端 REPL、流式输出、自动工具调用。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 依赖准备

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的三个包：

```powershell
$ver = '0.1.2'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

升级 ApiHub 时同步改动 `$ver` 与 `Kuroe.csproj` 中的包版本。
