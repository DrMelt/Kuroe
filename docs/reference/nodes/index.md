# 节点

流程节点按 `Kind` 区分类型。定义条目写 `Name` 与 `Kind`，引用条目写 `Label` 与 `Use`，结构与接线规则见[流程定义](../流程定义.md)。

| 类型        | 作用                     | 页面                      |
| ----------- | ------------------------ | ------------------------- |
| `Container` | 组合成员并限定作用域     | [容器](container.md)      |
| `Llm`       | 由模型执行               | [Llm](llm.md)             |
| `FileRead`  | 读取文件                 | [文件读取](file-read.md)  |
| `FileWrite` | 写入文件                 | [文件写入](file-write.md) |
| `Command`   | 执行命令并把输出作为产出 | [命令执行](command.md)    |
| `Input`     | 等用户回答               | [输入](input.md)          |
| `Approval`  | 等用户批准               | [审批](approval.md)       |

文件必须显式写 `Kind`。类型参数与结构字段同级写在节点对象里，引用节点不写类型参数。`Llm`、`Input` 与 `Approval` 接受 `From` 引入的未声明 `Text` 输入，其他类型要求目标已在 `Inputs` 声明。