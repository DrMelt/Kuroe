# Llm

`Kind: Llm` 是唯一调用模型的节点类型，派发执行并按端口交回产出。

## 端口

- 必须声明静态输入端口 `MODEL_NAME`，类型 `Text`，模式 `Constant`，角色 `Configuration`。标准写法是从开始端口绑定宿主输入 `MODEL`，见[模型接入](../../usage/模型接入.md)。
- 输入与输出端口取 `Text`、`Context` 与 `Signal`。`Signal` 未写 `Mode` 时默认 `Trigger`，其他类型默认 `Plain`。
- `Context` 输入置于目标输入最前。`Context` 输出由来源透传，不需要交回。
- `Llm` 接受 `From` 引入的未声明 `Text` 输入。

## 类型参数

| 参数           | 类型     | 含义                                                                    |
| -------------- | -------- | ----------------------------------------------------------------------- |
| `Prompt`       | string   | 本次执行的要求，与轮次一起构成本轮指令                                  |
| `MaxTurns`     | int      | 单次执行内最多向模型发起的请求次数，未写时不限制                        |
| `Tools`        | string[] | 工具白名单，按工具路径前缀匹配，见[工具配置](../工具配置.md#工具白名单) |
| `SystemPrompt` | string[] | 恒定系统指令块，按序拼接置于请求最前                                    |
| `ToolPorts`    | object   | 工具路径变量到输入端口的映射，见[工具变量](../流程定义.md#工具变量)     |

`SystemPrompt` 的内容必须恒定，才能命中服务商前缀缓存。空白块在导入时报错。

## 执行

节点在静态输入就绪或有 `From` 新产出时启动。`Prompt` 写在指令最前，第 2 轮起追加轮次说明。`Emission: Once` 的节点首次成功发布后冻结，`Repeated` 节点逐次发布；`MaxExecutions` 未写时为 100。

每次请求都带完整工具面。模型请求工具时，节点在同一执行内执行工具并把结果交回模型，直到模型不再请求工具。声明了 `MaxTurns` 时，单次执行向模型发起的请求达到该值后仍需继续请求则执行失败；未声明时不限制。

节点结束时经契约工具 `SubmitPortValues` 交回动态端口与由执行产出的常量端口。未交回时同一执行内追问最多两次，仍缺则停在阻塞待返工。`GetStatus` 与 `ReportCannotComplete` 始终可用，见[契约工具](../tools/contract.md)。

## 请求与用量

一次执行内的模型请求次数、token 用量与上下文随执行快照保留，执行详情与 `KuroeInfo/GetExecution` 可查。请求次数与 token 用量在运行中给出当前累计，收口后为最终值。

- 请求次数按真正发出的请求计，工具往返与契约追问各计一次。
- `MaxTurns` 是请求次数上限，超限时报实际次数与上限。
- token 用量取自 provider 返回的 usage，分输入、输出与合计，另记缓存输入与推理 token。
- 缓存输入是输入的子集，推理是输出的子集，都不重复计入合计。
- 上下文取最近一次返回用量的请求的输入 token 数，表示该请求发送的上下文规模，不随请求累计；该请求没有给出输入 token 数时显示未提供。
- provider 不返回 usage 时该请求计入未提供用量，展示为未提供，不按 0 计；此时清除上下文，显示未提供。

## 工具

`Tools` 只放行写出的路径子树，未写时不给出白名单内的工具，契约工具仍会附上。被放行且带路径变量的工具必须用 `ToolPorts` 完整映射，否则导入报错。工具分组与函数见[内置工具](../tools/index.md)。

```json
"Inputs": [{ "Name": "任务路径", "Type": "Text", "Mode": "Constant" }],
"Bindings": { "任务路径": "Start@TASK_WORK_PATH" },
"Tools": ["file/read", "GetLocalTime"],
"ToolPorts": { "file/read": { "root": "任务路径" } }
```

## 示例

```json
{
  "Name": "撰写",
  "Kind": "Llm",
  "Emission": "Once",
  "Prompt": "按目标撰写正文。",
  "SystemPrompt": ["回答使用简体中文。"],
  "Inputs": [
    { "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" },
    { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
  ],
  "Bindings": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
  "Outputs": [{ "Name": "正文", "Type": "Text", "Mode": "Constant" }]
}
```