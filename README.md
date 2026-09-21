# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的最小智能体骨架：终端 REPL、流式输出、自动工具调用。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 项目结构

- `Kuroe`：智能体会话、模型目录与配置的库，不涉及终端。偏好默认值在代码中，覆盖项写在工作目录下的 `settings.json`；作为库使用时由宿主给出工作目录并注册日志输出，其余装配由 `AddKuroe` 完成。
- `Kuroe.Cli`：终端入口，承载 REPL、斜杠命令与日志输出。

运行：

```powershell
dotnet run --project Kuroe.Cli -- -d <工作目录>
```

启动参数由 System.CommandLine 解析：`-h`/`--help` 打印用法，参数错误由它输出并以非零退出码结束；工作目录非法、装配失败这类启动校验错误仍由 Kuroe 以 `错误：<码>：<说明>` 的形式输出到 stderr。

首次运行时工作目录里还没有提供商与模型，用 `/provider add <名> <端点> <凭据>` 与 `/model add <模型> <提供商>` 登记。未选择模型不影响启动，发起对话时才会提示，用 `/model <模型>` 选择、`/model none` 取消选择。

斜杠命令的参数按空白拆分，双引号内的空白不拆分、引号本身不属于参数，如 `/set Agent:SystemPrompt "多 词"`、`/catalog export "D:\含 空格\catalog.json"`。

## 存储位置

`settings.json` 与 `catalog.json` 写在确定的工作目录下。前者是用户层偏好，后者是提供商、模型与凭据。工作目录按以下顺序确定：

1. 命令行 `--work-directory <目录>`，短名 `-d`，也接受 `--work-directory=<目录>`
2. 启动进程的当前目录

偏好配置只有两个来源：代码默认值与工作目录下的 `settings.json`，后者由 `/set`、`/unset` 写入，也可以手工编辑。路径按配置节的属性名逐级书写，如 `Agent:Temperature`：大小写不敏感，未定义的路径被拒绝，路径不会进入值对象内部。加载时设置项的键统一为属性名，同一个设置项写成多个大小写变体时程序拒绝启动。`Agent:Model` 由 `/model` 管理，`/set` 与 `/unset` 不接受。`catalog.json` 含明文凭据，不要提交。

## 依赖准备

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的三个包：

```powershell
$ver = '0.1.2'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

升级 ApiHub 时同步改动 `$ver` 与 `Directory.Packages.props` 中的包版本。
