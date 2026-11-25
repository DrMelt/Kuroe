# 执行能力

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    NodeTypes["节点类型<br>Kuroe/Ability/Node"]
    Runners["运行器<br>Kuroe/Ability/Runner"]
    ToolDomain["工具<br>Kuroe/Ability/Tool"]
    Container["容器节点类型<br>Kuroe.Flow/NodeType"]
    NodeTypes --> Runners
    NodeTypes --> ToolDomain
    Runners --> ToolDomain
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 节点类型 | `Kuroe.Ability.Node.Llm` | [kuroe-ability-node-llm](node/kuroe-ability-node-llm.md) |
| 节点类型 | `Kuroe.Ability.Node.Command` | [kuroe-ability-node-command](node/kuroe-ability-node-command.md) |
| 节点类型 | `Kuroe.Ability.Node.File` | [kuroe-ability-node-file](node/kuroe-ability-node-file.md) |
| 节点类型 | `Kuroe.Ability.Node.Input` | [kuroe-ability-node-input](node/kuroe-ability-node-input.md) |
| 节点类型 | `Kuroe.Ability.Node.Approval` | [kuroe-ability-node-approval](node/kuroe-ability-node-approval.md) |
| 运行器 | `Kuroe.Ability.Runner.Shared` | [kuroe-ability-runner-shared](runner/kuroe-ability-runner-shared.md) |
| 运行器 | `Kuroe.Ability.Runner.Llm` | [kuroe-ability-runner-llm](runner/kuroe-ability-runner-llm.md) |
| 运行器 | `Kuroe.Ability.Runner.Command` | [kuroe-ability-runner-command](runner/kuroe-ability-runner-command.md) |
| 运行器 | `Kuroe.Ability.Runner.File` | [kuroe-ability-runner-file](runner/kuroe-ability-runner-file.md) |
| 运行器 | `Kuroe.Ability.Runner.Input` | [kuroe-ability-runner-input](runner/kuroe-ability-runner-input.md) |
| 运行器 | `Kuroe.Ability.Runner.Approval` | [kuroe-ability-runner-approval](runner/kuroe-ability-runner-approval.md) |
| 工具 |  | [tool](tool/index.md) |
| 容器节点类型 | `Kuroe.Flow.NodeType.Container` | [kuroe-flow-node-type-container](container/kuroe-flow-node-type-container.md) |