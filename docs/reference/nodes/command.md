# 命令执行

`Kind: Command` 启动执行。`From` 任一组齐备且来源有新产出时在 `Directory` 内执行一次命令，合并 stdout 与 stderr 写入 `Output`，并在同一次发布中触发 `Completed`。命令在任务推进的独立执行中运行，不占用任务状态锁；暂停与终止会终止进程树。

## 类型参数

| 参数                    | 类型     | 含义                                                                               |
| ----------------------- | -------- | ---------------------------------------------------------------------------------- |
| `Program`               | string   | 程序名或路径，必填                                                                 |
| `Arguments`             | string[] | 传给程序的参数，逐项传入，不经 shell 解释，可含 `{变量}`                           |
| `Directory`             | string   | 执行目录，可含 `{变量}`，未写时在工作区根执行                                      |
| `Variables`             | object   | 命令变量名到 `Text` 输入端口的映射，与 `Directory` 和 `Arguments` 的占位符一一对应 |
| `TimeoutSeconds`        | int      | 等待秒数，未写时 120，必须是正整数                                                 |
| `MaxChars`              | int      | 输出字符上限，未写时 8000，0 不限制，负数在导入时报错                              |
| `FailOnNonZeroExitCode` | bool     | 退出码非零时是否停在阻塞，未写时不阻塞                                             |

## 端口

- `Inputs` 至少声明一个 `Signal` 端口作为触发。
- `Text` 输入全部是命令变量端口，不能声明承载内容的其他 `Text` 输入。
- `Outputs` 由类型给出，节点不声明，写了报错：`Output` 为 `Text`，承载合并输出；`Completed` 为 `Signal`，只表示本次执行完成，不代表退出码为零。

下游取命令输出写 `节点标签@Output`，只等待完成写 `节点标签@Completed`。

## 执行

执行目录与参数由命令变量展开。`root` 是相对路径的基准，`Directory` 中的 `root` 取工作区内的绝对路径，其他变量按原文替换后相对基准解析，展开结果必须落在工作区根内；未写 `Directory` 时在工作区根执行。`Arguments` 的变量按原文替换，不做路径校验，变量值可以是仓库地址、分支名等非路径文本。`Arguments` 中的花括号按占位符处理，字面花括号需要写成变量值。程序在解析后的目录启动，参数逐项传入，不经 shell 解释。命令不在交互模式下运行。

命令结束且未阻塞时把合并输出写入 `Output`，去掉末尾换行，超过 `MaxChars` 时截断并附截断标记，`Completed` 随同一次发布触发下游。退出码非零默认不影响产出，退出码本身不进入输出端口。声明 `FailOnNonZeroExitCode` 后，非零退出码停在阻塞，原因含退出码与输出。超时、路径越界或无法启动时停在阻塞，原因写入阻塞状态。超时后连同进程树终止。

## 示例

```json
{
  "Name": "构建",
  "Kind": "Command",
  "Program": "dotnet",
  "Arguments": ["build", "Kuroe.slnx"],
  "Directory": "{root}",
  "Variables": { "root": "工作路径" },
  "TimeoutSeconds": 600,
  "Inputs": [
    { "Name": "工作路径", "Type": "Text", "Mode": "Constant" },
    { "Name": "触发", "Type": "Signal" }
  ],
  "Bindings": { "工作路径": "Start@工作路径" },
  "From": [{ "触发": "计划@结论" }]
}
```

`Arguments` 从任务输入取仓库地址与分支名：

```json
{
  "Name": "克隆",
  "Kind": "Command",
  "Program": "git",
  "Arguments": ["clone", "--branch", "{branch}", "{repository}", "."],
  "Directory": "{root}",
  "Variables": {
    "root": "任务路径",
    "branch": "分支名",
    "repository": "仓库地址"
  },
  "Inputs": [
    { "Name": "任务路径", "Type": "Text", "Mode": "Constant" },
    { "Name": "分支名", "Type": "Text", "Mode": "Constant" },
    { "Name": "仓库地址", "Type": "Text", "Mode": "Constant" },
    { "Name": "触发", "Type": "Signal" }
  ],
  "Bindings": {
    "任务路径": "Start@任务路径",
    "分支名": "Start@分支名",
    "仓库地址": "Start@仓库地址"
  },
  "From": [{ "触发": "Start@要求" }]
}
```
