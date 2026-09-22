# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的最小智能体骨架：终端 REPL、流式输出、自动工具调用。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 项目结构

- `Kuroe`：智能体会话、模型目录与配置的库，不涉及终端。偏好默认值在代码中，覆盖项写在工作目录下的 `settings.json`；作为库使用时由宿主给出工作目录并注册日志输出，其余装配由 `AddKuroe` 完成。
- `Kuroe.Cli`：终端入口，承载 REPL、斜杠命令与日志输出。

## 对外边界

`Kuroe` 是宿主与外部世界之间唯一的边界：ApiHub 与 Microsoft.Extensions.AI 的包引用标为 `PrivateAssets="compile"`，对宿主隐藏编译可见性而照常随输出部署，宿主代码里出现这些类型时编译直接失败。宿主的编译面因此只有 `Kuroe` 的类型、`ErrorOr` 的错误形状，以及装配与日志所需的 `Microsoft.Extensions.DependencyInjection`、`Microsoft.Extensions.Logging`。

进出边界的都是文本与 `Kuroe` 自己的记录：提供商名、端点、凭据、模型名以字符串写入，目录快照、生效配置、改动影响以 `ProviderInfo`、`SettingEntry`、`SettingsEffect` 等读出。端点经 `Uri` 规范化，快照给出的是规范化后的文本，与录入写法可以不同。凭据原文不经快照交出，只给出它是真实凭据还是导出占位符。

目录内容的增删改查沿用 ApiHub 契约的错误码，如 `Catalog.ProviderAlreadyExists`、`Catalog.ProviderNotFound`、`Catalog.ModelNotFound`，宿主可按 `Error.Code` 判定；目录文件读写与路径错误是 `Kuroe` 自己的码，前缀为 `CatalogFile.`。

宿主注册自己的工具：实现 `IAgentTool`，在标注 `DescriptionAttribute` 的公开方法上承载能力，在 `AddKuroe` 之前或之后注册为 `IAgentTool` 实现都生效。

运行：

```powershell
dotnet run --project Kuroe.Cli -- -d <工作目录>
```

启动参数由 System.CommandLine 解析：`-h`/`--help` 打印用法，参数错误由它输出并以非零退出码结束；工作目录非法、装配失败这类启动校验错误仍由 Kuroe 以 `错误：<码>：<说明>` 的形式输出到 stderr。

首次运行时工作目录里还没有提供商与模型，用 `/provider add <名> <端点> <凭据>` 与 `/model add <模型> <提供商>` 登记。未选择模型不影响启动，发起对话时才会提示，用 `/model <模型>` 选择、`/model none` 取消选择。

斜杠命令的参数按空白拆分，双引号内的空白不拆分、引号本身不属于参数，如 `/set Agent:SystemPrompt "多 词"`、`/catalog export "D:\含 空格\导出.json"`。

## 存储位置

`settings.json` 与 `catalog.json` 写在确定的工作目录下。前者是用户层偏好，后者是提供商、模型与凭据。工作目录按以下顺序确定：

1. 命令行 `--work-directory <目录>`，短名 `-d`，也接受 `--work-directory=<目录>`
2. 启动进程的当前目录

`/catalog export` 与 `/catalog import` 的文件参数以工作目录为基准解析成绝对路径，绝对路径按规范化后的位置使用，成功提示回显解析结果。导出目标不能是 `catalog.json` 本身，导出内容是脱敏后的目录，覆盖它等于抹掉其中全部凭据。

偏好配置只有两个来源：代码默认值与工作目录下的 `settings.json`，后者由 `/set`、`/unset` 写入，也可以手工编辑。路径按配置节的属性名逐级书写，如 `Agent:Temperature`：大小写不敏感，未定义的路径被拒绝，路径不会进入值对象内部。加载时设置项的键统一为属性名，同一个设置项写成多个大小写变体时程序拒绝启动。`Agent:Model` 由 `ModelService` 独占，按路径写入一律被库层拒绝，`/set` 与 `/unset` 因此不接受它。`catalog.json` 含明文凭据，不要提交。

## 依赖准备

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的四个包：

```powershell
$ver = '0.2.0'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json', 'ApiHub.Shared' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

升级 ApiHub 时同步改动 `$ver` 与 `Directory.Packages.props` 中的包版本。
