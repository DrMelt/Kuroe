# Kuroe

基于 .NET 10 + Microsoft.Extensions.AI 的智能体骨架：终端 REPL、流式输出、自动工具调用，多个任务并发执行。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 特性

- 回复逐字流式输出，`Ctrl+C` 只中断当前一轮
- 多个任务同时执行，任务内的 agent 由流程步骤派出，互相不共享上下文
- 任务流程可配置：规划、实施、检查都只是流程里的步骤，由同一类 agent 承担
- 实施之后必有检查，检查不通过按配置返工，规则在加载流程时校验
- 查看以任务为单位：任务列表 → 任务内已执行与在执行的 agent → agent 的上下文来源与过程记录
- 工具调用自动发起，调用参数与结果分行呈现
- 提供商与模型在命令行登记，目录可脱敏导出、合并导入
- 偏好改动立即生效，需要重启或会使旧上下文失效时当场提示
- 内置时间、天气两个示例工具，实现 `IAgentTool` 声明函数即可扩展

## 环境要求

.NET SDK 10.0，以及可读键盘的交互终端——任务的逐级下钻用键盘选择，输入被重定向时启动即拒绝。

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

`-d` 省略时用启动进程的当前目录。`settings.json`、`catalog.json` 与 `flows.json` 写在确定的工作目录下，其中 `catalog.json` 含明文凭据，不要提交。命令里的文件参数（`/catalog export|import`、`/flow add`）相对该工作目录解析，与启动时的进程当前目录无关。

启动参数由 System.CommandLine 解析，`-h` 打印用法，参数错误以非零退出码结束。

## 首次配置

工作目录里还没有提供商与模型，先登记再接通：

```text
/provider add openai https://api.openai.com/v1 sk-xxxx
/model add gpt-4o-mini openai
/model gpt-4o-mini
/task new 查明北京今天的天气，给出穿衣建议
/task
exit
```

未选择模型不影响启动，提交任务与发起对话时才会提示。普通输入是**当前任务**里的一轮对话，所以先要有任务；`/task new` 提交的任务按流程派 agent。

## 任务与流程

任务是长期会话线程：它有自己的对话历史，也是前台对话的落点。提交任务时按流程模板建任务，流程的每个步骤由一个 agent 承担，agent 的上下文由上游装配后传入，不继承未声明的内容。

内置流程是规划 → 实施 → 检查：规划交回条目拆分，实施按条目各派一个 agent，检查逐个核对实施产出。三者都是同类 agent，差别只在角色与所处步骤。检查不通过时按检查步骤的配置退回返工，新实施 agent 的上下文里带上检查意见与上一轮产出。

`/flow show <流程>` 打印步骤的角色、展开方式、上游引用与放行规则；`Gate: Review` 的步骤产出后停在待批准，`/task approve <任务号>` 才开下一步。

## 查看

`/task` 打开浏览器，任务列表 → 任务内的步骤与 agent → 单个 agent 的详情，逐级进入，`Esc` 或选"返回"退一级。列表同时给出在跑与已派的 agent 数，详情里能跳到装配它时引用的上游 agent。

后台 agent 的过程不往终端里插，只在状态变化时打一行通知；`/task show <任务号>` 与 `/task agent <agent号>` 是不进浏览器的等价查看方式。任务的 agent 停在等待批准或失败时，浏览器里直接给出批准、返工、取消这几个动作。

## 斜杠命令

| 命令 | 说明 |
| --- | --- |
| `/help` | 打印命令列表 |
| `/reset` | 清空当前任务的上下文 |
| `/task` | 打开任务浏览器，逐级进入 agent 详情 |
| `/task list` | 列出任务 |
| `/task new <目标>` | 提交任务，按默认流程开第一步 |
| `/task new --flow <流程> <目标>` | 用指定流程提交任务 |
| `/task show <任务号>` | 打印该任务的步骤与 agent |
| `/task agent <agent号>` | 打印该 agent 的上下文来源与过程 |
| `/task use <任务号>` | 把前台对话切到该任务 |
| `/task title <任务号> <文本>` | 改任务标题 |
| `/task approve <任务号>` | 批准等待放行的步骤 |
| `/task rework <任务号>` | 返工被阻塞的单元 |
| `/task adopt <agent号>` | 把 agent 结论写进任务历史 |
| `/task stop <任务号>` | 取消任务 |
| `/task stop agent <agent号>` | 取消单个 agent |
| `/task clear` | 丢掉已完成或已取消的任务 |
| `/flow list` | 列出流程模板 |
| `/flow show <流程>` | 打印流程的步骤 |
| `/flow add <文件>` | 导入流程，同名覆盖 |
| `/flow default <流程>` | 设为提交任务的默认流程 |
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
    "LogLevel": "Information",
    "MaxConcurrentRuns": 4,
    "DefaultFlow": "默认",
    "MaxAttempts": 3
  }
}
```

`/set` 的值先按 JSON 字面量解析，不是合法 JSON 时按字符串写入。`Agent:Model` 只由 `/model` 维护，`Agent:DefaultFlow` 只由 `/flow default` 维护。

`MaxConcurrentRuns` 限制同时在跑的 agent 数，超出的排队等待，改动对后续派生即时生效；`MaxAttempts` 是单个条目允许的实施轮数上限，流程里的 `MaxAttempts` 不得超过它。

`DefaultFlow` 未写入用户层时用内置流程「默认」；写成不存在的名字，提交任务时才报「没有名为 X 的流程」。

## 流程配置

流程模板存在工作目录的 `flows.json`，可以手工编辑，也可以 `/flow add <文件>` 导入。文件不存在时用内置的"默认"流程。每条流程是一串步骤，步骤字段：

| 字段 | 含义 |
| --- | --- |
| `Name` | 步骤名，流程内唯一且不能为空 |
| `Role` | `Plan`、`Implement`、`Check` 之一，决定该步骤能交回什么 |
| `Model` | 该步骤用的模型，省略时用提交任务时选中的模型 |
| `Prompt` | 该步骤对模型的额外要求，与目标一起构成指令 |
| `Scope` | `Single` 整步一个 agent，`PerItem` 按规划交回的条目各派一个 |
| `From` | 上下文取自哪些更早的步骤产出 |
| `Gate` | `Auto` 产出即开下一步，`Review` 停在待批准 |
| `OnReject` | 检查不通过时 `Stop` 或 `Retry`，只能写在检查步骤上 |
| `MaxAttempts` | 允许的实施轮数，只能写在检查步骤上 |

```json
{
  "Flows": [
    {
      "Name": "默认",
      "Description": "内置流程：规划、实施、检查",
      "Steps": [
        { "Name": "规划", "Role": "Plan", "Prompt": "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。" },
        { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
        { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["规划", "实施"], "OnReject": "Retry", "MaxAttempts": 2 }
      ]
    }
  ]
}
```

启动时逐条校验，全部非法项一次给出。规则即任务推进依赖的不变量：步骤名不能为空且流程内唯一；`From` 只能引用更早的步骤；一条流程最多一个规划步骤，按条目展开的步骤必须排在其后，且其后的步骤同样按条目展开；实施之后要有检查，检查步骤必须用 `From` 引用被它检查的实施产出。

## 限制

超出时不报错，按下列方式收口，都不需要通过配置调整：

| 位置 | 上限 | 超出后 |
| --- | --- | --- |
| 过程记录 | 400 条 | 丢最早的记录，`/task show` 与 agent 详情里标出被丢的条数 |
| 详情视图里的过程记录 | 最近 40 条 | 更早的只报条数 |
| 过程记录里的一段文本 | 4096 字 | 另起一段，避免逐字增量反复拷贝整段 |
| 装配进上下文的单条内容 | 2000 字 | 截断并加省略号 |
| 带进上下文的任务对话 | 最近 6 条 | 更早的内容由上游产出概括 |
| 任务标题 | 40 字 | 取目标首行截断 |
| 一个方案的条目 | 20 条 | 提交被拒绝，模型在同一轮里改正 |

## 开发

```powershell
dotnet build Kuroe.slnx
dotnet test Kuroe.slnx
```

## 许可

AGPL-3.0-only，见 `LICENSE`。

