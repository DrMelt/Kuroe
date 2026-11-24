# 流程实现

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Graph["图流程<br>Kuroe.Flow/{Control,Graph,Records}"]
    Maf["MAF 流程<br>Kuroe.Maf/{Control,Definition,Records}"]
```

| 项目 | 节点 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Flow.Control` | 图流程 | [kuroe-flow-control](graph/kuroe-flow-control.md) |
| `Kuroe.Flow.Graph.Shared` | 图流程 | [kuroe-flow-graph-shared](graph/kuroe-flow-graph-shared.md) |
| `Kuroe.Flow.Records.Shared` | 图流程 | [kuroe-flow-records-shared](graph/kuroe-flow-records-shared.md) |
| `Kuroe.Maf.Control` | MAF 流程 | [kuroe-maf-control](maf/kuroe-maf-control.md) |
| `Kuroe.Maf.Definition` | MAF 流程 | [kuroe-maf-definition](maf/kuroe-maf-definition.md) |
| `Kuroe.Maf.Records.Shared` | MAF 流程 | [kuroe-maf-records-shared](maf/kuroe-maf-records-shared.md) |