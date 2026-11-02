# 契约工具

执行节点由引擎自动附上契约工具，不需要写进 `Tools` 白名单。

| 路径                   | 参数         | 作用                                                 |
| ---------------------- | ------------ | ---------------------------------------------------- |
| `SubmitPortValues`     | `valuesJson` | 提交本轮命名输出端口的值                             |
| `GetStatus`            | 无           | 查看节点、轮次、状态、尚缺端口与已上报的无法完成原因 |
| `ReportCannotComplete` | `reason`     | 上报本轮无法完成并给出原因                           |

## SubmitPortValues

声明 `Text`、`Signal` 输出端口或由执行产出常量端口的执行节点自动附上 `SubmitPortValues`。动态 `Context` 输出由来源透传，不附这个工具。

`valuesJson` 是端口名到值的 JSON 对象，端口必须与需交回的端口一一对应：

- `Text` 端口交回文本。
- `Signal` 端口交回空文本或端口名。
- 承载触发的 `Text` 端口交回文本，或带 `text` 与 `trigger` 的对象。
- 承载触发的 `Signal` 端口交回 `true` 或 `false`，或带 `trigger` 的对象。

`trigger` 省略时按 `true`，`false` 表示本次产出只被下游启动组消耗、不触发下游节点。

## 其他工具

`GetStatus` 只读当前执行契约，不改变状态。`ReportCannotComplete` 只在确实做不了时调用，能完成时直接结束本轮即可。

未交回全部端口时同一执行内追问最多两次，仍缺则停在阻塞待返工，见[Llm](../nodes/llm.md)。