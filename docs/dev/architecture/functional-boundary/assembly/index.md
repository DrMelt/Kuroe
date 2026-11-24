# 宿主装配

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Composition["核心装配<br>Kuroe/Composition"]
    GraphFlow["图流程装配<br>Kuroe/GraphFlow"]
    Maf["MAF 装配与节点执行装配<br>Kuroe.Maf/{Core,NodeExecution}"]
    Assistant["内置助手<br>Kuroe/Assistant"]
    GraphFlow --> Composition
    Assistant --> Composition
```

| 项目 | 节点 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Composition` | 核心装配 | [kuroe-composition](composition/kuroe-composition.md) |
| `Kuroe.GraphFlow` | 图流程装配 | [kuroe-graph-flow](graph-flow/kuroe-graph-flow.md) |
| `Kuroe.Maf` | MAF 装配与节点执行装配 | [kuroe-maf](maf/kuroe-maf.md) |
| `Kuroe.Maf.NodeExecution` | MAF 装配与节点执行装配 | [kuroe-maf-node-execution](maf/kuroe-maf-node-execution.md) |
| `Kuroe.Assistant` | 内置助手 | [kuroe-assistant](assistant/kuroe-assistant.md) |