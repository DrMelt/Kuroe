# 命令执行

`Kind: Command` 不启动执行。`From` 任一组齐备且来源有新产出时在 `Directory` 内执行一次命令，合并 stdout 与 stderr 写入唯一的 `Text` 输出端口。

## 类型参数

| 参数                    | 类型     | 含义                                                                |
| ----------------------- | -------- | ------------------------------------------------------------------- |
| `Program`               | string   | 程序名或路径，必填                                                  |
| `Arguments`             | string[] | 传给程序的参数，逐项传入，不经 shell 解释                           |
| `Directory`             | string   | 执行目录，可含 `{变量}`，未写时在工作区根执行                       |
| `Variables`             | object   | 路径变量名到 `Text` 输入端口的映射，与 `Directory` 的占位符一一对应 |
| `TimeoutSeconds`        | int      | 等待秒数，未写时 120，必须是正整数                                  |
| `MaxChars`              | int      | 输出字符上限，未写时 8000，0 不限制，负数在导入时报错               |
| `FailOnNonZeroExitCode` | bool     | 退出码非零时是否停在阻塞，未写时不阻塞                              |

## 端口

- `Inputs` 至少声明一个 `Signal` 端口作为触发。
- `Text` 输入全部是路径变量端口，不能声明承载内容的其他 `Text` 输入。
- `Outputs` 必须恰好声明一个 `Text` 端口，命令输出写入该端口。

## 执行

执行目录由路径变量解析，未写 `Directory` 时在工作区根执行，解析结果必须落在工作区根内。程序在解析后的目录启动，参数逐项传入，不经 shell 解释。命令不在交互模式下运行。

成功时交回合并输出，去掉末尾换行，超过 `MaxChars` 时截断并附截断标记。退出码非零默认不影响产出，退出码本身不进入输出端口。声明 `FailOnNonZeroExitCode` 后，非零退出码停在阻塞，原因含退出码与输出。超时、路径越界或无法启动时停在阻塞，原因写入阻塞状态。超时后连同进程树终止。

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
  "Bindings": { "工作路径": "Start@WORKSPACE_PATH" },
  "From": [{ "触发": "计划@结论" }],
  "Outputs": [{ "Name": "输出", "Type": "Text" }]
}
```