# 执行能力

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    NodeTypes["节点类型<br>Kuroe/Node"]
    Runners["运行器<br>Kuroe/Runner"]
    ToolDomain["工具<br>Kuroe/Tool"]
    Container["容器节点类型<br>Kuroe.Flow/NodeType"]
    NodeTypes --> Runners
    NodeTypes --> ToolDomain
    Runners --> ToolDomain
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 节点类型 | `Kuroe.Node.Llm` | [kuroe-node-llm](node/kuroe-node-llm.md) |
| 节点类型 | `Kuroe.Node.Command` | [kuroe-node-command](node/kuroe-node-command.md) |
| 节点类型 | `Kuroe.Node.File` | [kuroe-node-file](node/kuroe-node-file.md) |
| 节点类型 | `Kuroe.Node.Input` | [kuroe-node-input](node/kuroe-node-input.md) |
| 节点类型 | `Kuroe.Node.Approval` | [kuroe-node-approval](node/kuroe-node-approval.md) |
| 运行器 | `Kuroe.Runner.Shared` | [kuroe-runner-shared](runner/kuroe-runner-shared.md) |
| 运行器 | `Kuroe.Runner.Llm` | [kuroe-runner-llm](runner/kuroe-runner-llm.md) |
| 运行器 | `Kuroe.Runner.Command` | [kuroe-runner-command](runner/kuroe-runner-command.md) |
| 运行器 | `Kuroe.Runner.File` | [kuroe-runner-file](runner/kuroe-runner-file.md) |
| 运行器 | `Kuroe.Runner.Input` | [kuroe-runner-input](runner/kuroe-runner-input.md) |
| 运行器 | `Kuroe.Runner.Approval` | [kuroe-runner-approval](runner/kuroe-runner-approval.md) |
| 工具 |  | [tool](tool/index.md) |
| 容器节点类型 | `Kuroe.Flow.NodeType.Container` | [kuroe-flow-node-type-container](container/kuroe-flow-node-type-container.md) |