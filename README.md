# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的最小智能体骨架：终端 REPL、流式输出、自动工具调用。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 特性

- 回复逐字流式输出，`Ctrl+C` 只中断当前一轮
- 工具调用自动发起，调用参数与结果分行呈现
- 提供商与模型在命令行登记，目录可脱敏导出、合并导入
- 偏好改动立即生效，需要重启或会使旧上下文失效时当场提示
- 内置时间、天气两个示例工具，实现 `IAgentTool` 即可扩展

## 环境要求

.NET SDK 10.0。

## 依赖准备

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的四个包：

```powershell
$ver = '0.2.0'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json', 'ApiHub.Shared' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

升级 ApiHub 时同步改动 `$ver` 与 `Directory.Packages.props` 中的包版本。

## 运行

```powershell
dotnet run --project Kuroe.Cli -- -d <工作目录>
```

`-d` 省略时用启动进程的当前目录。`settings.json` 与 `catalog.json` 写在确定的工作目录下，后者含明文凭据，不要提交。

启动参数由 System.CommandLine 解析，`-h` 打印用法，参数错误以非零退出码结束。

## 首次配置

工作目录里还没有提供商与模型，先登记再接通：

```text
/provider add openai https://api.openai.com/v1 sk-xxxx
/model add gpt-4o-mini openai
/model gpt-4o-mini
你好，现在几点？
exit
```

未选择模型不影响启动，发起对话时才会提示。

## 斜杠命令

| 命令 | 说明 |
| --- | --- |
| `/help` | 打印命令列表 |
| `/reset` | 清空上下文 |
| `/config` | 打印生效配置，标记各项是否已写入用户层 |
| `/set <路径> <值>` | 写入用户层并立即生效 |
| `/unset <路径>` | 删除用户层中的该项 |
| `/provider list` | 列出提供商 |
| `/provider add <名> <端点> <凭据>` | 新增提供商 |
| `/provider key <名> <凭据>` | 更换提供商凭据 |
| `/provider rm <名>` | 删除提供商，仍被模型引用时拒绝 |
| `/model` | 打印当前模型 |
| `/model list` | 列出已注册模型 |
| `/model <模型>` | 切换模型，要求已注册 |
| `/model none` | 取消选择模型 |
| `/model add <模型> <提供商>` | 把模型注册到提供商 |
| `/model rm <模型>` | 注销模型，是当前模型时选择一并取消 |
| `/catalog list` | 列出提供商与模型 |
| `/catalog export <文件>` | 导出目录，凭据替换为占位符 |
| `/catalog import <文件>` | 合并导入目录 |

`exit` 不是斜杠命令，直接输入即可退出。命令参数按空白拆分，双引号内的空白算一个参数、引号本身不算，如 `/set Agent:SystemPrompt "多 词"`。

## 配置

偏好只有两个来源：代码默认值与工作目录下的 `settings.json`，后者由 `/set`、`/unset` 写入，也可以手工编辑。路径按 `<节>:<设置项>` 书写，如 `Agent:Temperature`，大小写不敏感，未定义的路径被拒绝。

```json
{
  "Agent": {
    "SystemPrompt": "你是一个可以使用工具获取实时信息并解答问题的助手。",
    "Temperature": 0.7,
    "MaxOutputTokens": 1024,
    "LogLevel": "Information"
  }
}
```

`/set` 的值先按 JSON 字面量解析，不是合法 JSON 时按字符串写入。`Agent:Model` 只由 `/model` 维护。

## 作为库使用

`Kuroe` 是库，`Kuroe.Cli` 是它的终端宿主。宿主给出工作目录与日志输出，其余装配由 `services.AddKuroe(KuroePaths.At(directory))` 完成，失败时一次返回全部启动期错误。

ApiHub 与 Microsoft.Extensions.AI 的引用标为 `PrivateAssets="compile"`，宿主只见 `Kuroe` 的类型、`ErrorOr` 的错误形状，以及装配与日志所需的 `Microsoft.Extensions.DependencyInjection` 与 `Microsoft.Extensions.Logging`。工具实现 `IAgentTool`，在标注 `DescriptionAttribute` 的公开方法上承载能力，在 `AddKuroe` 之前或之后注册都生效。

## 开发

```powershell
dotnet build Kuroe.slnx
```

## 许可

AGPL-3.0-only，见 `LICENSE`。

