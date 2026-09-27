# Kuroe

基于 .NET 10 + Microsoft Agent Framework 与 Microsoft.Extensions.AI 的智能体骨架：终端 REPL、流式输出、自动工具调用，多个任务并发执行。模型接入与流程编排分别落在上层调用与图执行器上，会话与工具调用由框架承担。流程由节点组织：执行节点自带模型选择与工具白名单并声明产出契约，容器组合执行节点并限定作用域。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 特性

- 回复逐字流式输出，`Ctrl+C` 只中断当前一轮
- 多个任务同时执行，任务内的 run 由流程执行节点派出，互相不共享上下文
- 任务流程可配置：制定计划、分配执行、整体检查都只是流程里的节点，每个执行节点声明自己的模型选择、能力工具与指令
- 按条目展开实施，实施完成后整体检查，检查不通过按配置退回返工，规则在加载流程时校验
- 查看以任务为单位：任务列表 → 任务内已执行与在执行的 run → run 的上下文来源与过程记录
- 工具调用自动发起，调用参数与结果分行呈现
- 提供商与模型在命令行登记，目录可脱敏导出、合并导入
- 偏好改动立即生效，需要重启或会使旧上下文失效时当场提示
- 内置时间、天气两个示例工具，实现 `ITool` 声明函数即可扩展

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

`Microsoft.Agents.AI` 与 `Microsoft.Agents.AI.Workflows` 从 nuget.org 获取，版本与 `Directory.Packages.props` 保持一致。升级 ApiHub 时同步改动 `$ver` 与其中的包版本。

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

未选择模型不影响启动，提交任务与发起对话时才会提示。普通输入是**当前任务**里的一轮对话，所以先要有任务；`/task new` 提交的任务按流程派 run。

## 任务与流程

任务是长期会话线程：它有自己的对话历史，也是前台对话的落点。提交任务时按流程模板建任务，流程由节点组成：执行节点自带模型选择、能力工具与指令，run 的上下文由上游装配后传入，不继承未声明的内容。

内置流程是制定计划 → 分配执行 → 整体检查：制定计划交回条目拆分，分配执行按条目各派一个 run，整体检查汇总全部实施产出交回结论。三者由「规划者」「执行者」「检查者」三个模型配置承担，差别的只是被执行节点引用的位置与产出契约，能力工具写在执行节点上。整体检查不通过时按其配置退回返工，新实施 run 的上下文里带上整体检查意见与上一轮产出。

`/flow show <流程>` 打印节点的层级、产出的契约、展开方式、上游引用与放行规则；`Gate: Review` 的执行节点产出后停在待批准，`/task approve <任务号>` 才开下一步。

## 查看

`/task` 打开浏览器，任务列表 → 任务内的执行节点与 run → 单个 run 的详情，逐级进入，`Esc` 或选"返回"退一级。列表同时给出在跑与已派的 run 数，详情里能跳到装配它时引用的上游 run。

后台 run 的过程不往终端里插，只在状态变化时打一行通知；`/task show <任务号>` 与 `/task run <run号>` 是不进浏览器的等价查看方式。任务停在等待批准或失败时，浏览器里直接给出批准、返工、取消这几个动作。

## 斜杠命令

| 命令 | 说明 |
| --- | --- |
| `/help` | 打印命令列表 |
| `/reset` | 清空当前任务的上下文 |
| `/task` | 打开任务浏览器，逐级进入 run 详情 |
| `/task list` | 列出任务 |
| `/task new <目标>` | 提交任务，按默认流程开第一步 |
| `/task new --flow <流程> <目标>` | 用指定流程提交任务 |
| `/task show <任务号>` | 打印该任务的节点与 run |
| `/task run <run号>` | 打印该 run 的上下文来源与过程 |
| `/task use <任务号>` | 把前台对话切到该任务 |
| `/task title <任务号> <文本>` | 改任务标题 |
| `/task approve <任务号>` | 批准等待放行的节点 |
| `/task rework <任务号>` | 返工被阻塞的单元 |
| `/task adopt <run号>` | 把 run 结论写进任务历史 |
| `/task stop <任务号>` | 取消任务 |
| `/task stop run <run号>` | 取消单个 run |
| `/task clear` | 丢掉已完成或已取消的任务 |
| `/flow list` | 列出流程模板 |
| `/flow show <流程>` | 打印流程的节点 |
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

`exit` 不是斜杠命令，直接输入即可退出。命令参数按空白拆分，双引号内的空白算一个参数、引号本身不算，如 `/set Runtime:Temperature "多 词"`。

## 配置

偏好只有两个来源：代码默认值与工作目录下的 `settings.json`，后者由 `/set`、`/unset` 写入，也可以手工编辑。路径按 `<节>:<设置项>` 书写，如 `Runtime:Temperature`，大小写不敏感，未定义的路径被拒绝。

```json
{
  "Runtime": {
    "Temperature": 0.7,
    "MaxOutputTokens": 1024,
    "LogLevel": "Information",
    "MaxConcurrentRuns": 4,
    "DefaultFlow": "默认",
    "MaxAttempts": 3
  }
}
```

`/set` 的值先按 JSON 字面量解析，不是合法 JSON 时按字符串写入。`Runtime:Model` 只由 `/model` 维护，`Runtime:DefaultFlow` 只由 `/flow default` 维护。

`MaxConcurrentRuns` 限制同时在跑的 run 数，超出的排队等待，改动对后续派生即时生效；`MaxAttempts` 是单个条目允许的实施轮数上限，流程里的 `MaxAttempts` 不得超过它。

`DefaultFlow` 未写入用户层时用内置流程「默认」；写成不存在的名字，提交任务时才报「没有名为 X 的流程」。

## 流程配置

流程模板存在工作目录的 `flows.json`，可以手工编辑，也可以 `/flow add <文件>` 导入。文件不存在时用内置的"默认"流程。每条流程由命名的模型选择与节点树组成：执行节点引用一个模型选择、声明能力工具白名单与产出契约，容器组合执行节点并为子节点限定作用域。模型选择字段：

| 字段 | 含义 |
| --- | --- |
| `Name` | 配置名，流程内唯一且不能为空 |
| `Model` | 使用的模型名，省略时用提交任务时选中的模型 |

节点字段：

| 字段 | 含义 |
| --- | --- |
| `Name` | 节点名，流程内唯一且不能为空 |
| `Model` | 执行节点引用的模型选择名，必须定义在同文件的 `Models` 里 |
| `Tools` | 能力工具白名单，按函数名匹配，省略或空时不给出任何能力工具 |
| `Output` | `Plain` 文本、`Plan` 交条目拆分、`Review` 交检查结论，决定能交回什么 |
| `Prompt` | 该节点对模型的额外要求，与目标一起构成指令 |
| `Mode` | 执行节点用 `Single` 整节点一个 run、`PerItem` 按拆分条目各派一个；执行节点树上不再有容器并行模式，并行改用 `Branch` 声明 |
| `Branch` | 本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目；与前一条一起构成并行分支 |
| `From` | 执行依赖的输入边：引用的执行节点必须先于本节点产出，多个来源的产出会一并进入本节点的上下文 |
| `Gate` | `Auto` 产出即开下一步，`Review` 停在待批准 |
| `OnReject` | 检查不通过时 `Stop` 或 `Retry`，只能写在检查执行节点上 |
| `MaxAttempts` | 允许的检查轮数，只能写在检查执行节点上 |
| `Split` | 拆分源的固定配置：`Items` 静态条目清单、`ExtrasMax` 模型补充上限、`Acceptance` 统一验收文本，只能写在规划执行节点上 |
| `Nodes` | 容器节点的有序子节点，至少一个 |

执行节点与执行节点之间的依赖由 `From` 直接表达：引用的执行节点必须先于本节点产出，多个来源的产出会一并进入上下文；同一个来源可以派生出多个下游并行推进。按条目展开的执行节点从它引用的规划执行节点取条目清单，每条目一个 run；声明 `Branch` 时只处理拆分中归属该分支的条目。分支并行即多个声明不同 `Branch` 的执行节点引用同一个规划执行节点，收拢检查引用它们并等全部完成。

```json
{
  "Flows": [
    {
      "Name": "整体检查",
      "Description": "制定计划、按条目分配执行、整体检查",
      "Models": [
        { "Name": "规划者" },
        { "Name": "执行者" },
        { "Name": "检查者" }
      ],
      "Nodes": [
        { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目。" },
        { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime", "GetWeather"], "Mode": "PerItem", "From": ["制定计划"] },
        { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
      ]
    },
    {
      "Name": "并行实施",
      "Description": "规划后分两条分支并行实施，整体检查",
      "Models": [
        { "Name": "规划者" },
        { "Name": "实施者" },
        { "Name": "检查者" }
      ],
      "Nodes": [
        { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
        { "Name": "撰写", "Model": "实施者", "Mode": "PerItem", "Branch": "撰写", "From": ["制定计划"] },
        { "Name": "排版", "Model": "实施者", "Mode": "PerItem", "Branch": "排版", "From": ["制定计划"] },
        { "Name": "整体检查", "Model": "检查者", "Output": "Review", "From": ["制定计划", "撰写", "排版"], "OnReject": "Retry" }
      ]
    }
  ]
}
```

启动时逐条校验，全部非法项一次给出。规则即任务推进依赖的不变量：节点名不能为空且流程内唯一，模型配置名不能为空且流程内唯一；节点引用的模型选择必须存在；`From` 引用必须存在，容器节点不可被引用，容器也不可声明 `From` 与 `Prompt`；引用关系必须无环；按条目展开的执行节点必须从规划执行节点取拆分或从其它展开执行节点对齐输入，声明 `Branch` 的执行节点必须按条目展开并从规划执行节点取拆分；每条实施必须被某个检查执行节点用 `From` 引用，检查执行节点必须引用被检查的实施；流程必须有检查执行节点；`Split` 只写在规划执行节点上，`OnReject` 与 `MaxAttempts` 只写在检查执行节点上。

## 限制

超出时不报错，按下列方式收口，都不需要通过配置调整：

| 位置 | 上限 | 超出后 |
| --- | --- | --- |
| 过程记录 | 400 条 | 丢最早的记录，`/task show` 与 run 详情里标出被丢的条数 |
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

