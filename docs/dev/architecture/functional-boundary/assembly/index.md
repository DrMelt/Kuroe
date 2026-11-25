# 宿主装配

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Composition["核心服务与装配<br>Kuroe/Composition"]
    GraphFlow["图流程装配<br>Kuroe/FlowIntegration/GraphFlow"]
    Maf["MAF 装配<br>Kuroe/FlowIntegration/Maf"]
    Assistant["内置助手<br>Kuroe/Assistant"]
    GraphFlow --> Composition
    Assistant --> Composition
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 核心服务与装配 | `Kuroe.Composition` | [kuroe-composition](composition/kuroe-composition.md) |
| 图流程装配 | `Kuroe.FlowIntegration.GraphFlow` | [kuroe-flow-integration-graph-flow](graph-flow/kuroe-flow-integration-graph-flow.md) |
| MAF 装配 | `Kuroe.FlowIntegration.Maf` | [kuroe-flow-integration-maf](maf/kuroe-flow-integration-maf.md) |
| 内置助手 | `Kuroe.Assistant` | [kuroe-assistant](assistant/kuroe-assistant.md) |