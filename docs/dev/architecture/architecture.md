# Kuroe 总体架构

Kuroe 把用户定义的流程交给模型执行。

项目结构与边界文档见[项目结构](functional_boundary/index.md)。

## 命名空间与路径

命名空间根与项目名一致，程序集内按目录继续分段。`Kuroe.GraphFlow` 分为 `HostInputs` 与 `NodeExecution` 两支。`Kuroe.Runner.Shared` 是唯一例外，命名空间根为 `Kuroe.Runner`。`Kuroe.Flow.Graph.Shared` 分为 `StaticGraph`、`RuntimeGraph`、`Runtime.Nodes` 与 `Graph.Records` 四支，`RuntimeGraph` 之下按执行、节点、端口、就绪与任务分段，`Kuroe.FlowDescription.Description.Shared` 分为项目根与 `Reporting`，`Kuroe.FlowDescription.Definition.Shared` 分为 `Model`、`Flow`、`Nodes`、`Ports` 与 `Validation`，`Kuroe.FlowDescription.Compilation.Shared` 分为项目根、`Flow` 与 `NodeTypes`，`Kuroe.FlowDescription.Compilation` 的命名空间在项目根之下按目录分段，`Kuroe.Flow.Control` 的命名空间全部在 `Kuroe.Flow.Control.Runtime` 之下。

项目名与目录的推导规则见[架构测试](../architecture-tests.md#项目与目录约束)。

引用约束由架构测试固定，见[架构测试](../architecture-tests.md)。
