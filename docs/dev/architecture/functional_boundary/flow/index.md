# 流程实现

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Graph["图流程<br>Kuroe.Flow/{Compilation,Control,Graph,Library,Records}"]
    Maf["MAF 流程<br>Kuroe.Maf/{Compilation,Control,Definition,Records}"]
```

| 项目 | 节点 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Flow.Compilation` | 图流程 | [kuroe-flow-compilation](graph/kuroe-flow-compilation.md) |
| `Kuroe.Flow.Control` | 图流程 | [kuroe-flow-control](graph/kuroe-flow-control.md) |
| `Kuroe.Flow.Graph.Shared` | 图流程 | [kuroe-flow-graph-shared](graph/kuroe-flow-graph-shared.md) |
| `Kuroe.Flow.Library` | 图流程 | [kuroe-flow-library](graph/kuroe-flow-library.md) |
| `Kuroe.Flow.Library.Files` | 图流程 | [kuroe-flow-library-files](graph/kuroe-flow-library-files.md) |
| `Kuroe.Flow.Records.Shared` | 图流程 | [kuroe-flow-records-shared](graph/kuroe-flow-records-shared.md) |
| `Kuroe.Maf.Compilation.Shared` | MAF 流程 | [kuroe-maf-compilation-shared](maf/kuroe-maf-compilation-shared.md) |
| `Kuroe.Maf.Compilation` | MAF 流程 | [kuroe-maf-compilation](maf/kuroe-maf-compilation.md) |
| `Kuroe.Maf.Control` | MAF 流程 | [kuroe-maf-control](maf/kuroe-maf-control.md) |
| `Kuroe.Maf.Definition` | MAF 流程 | [kuroe-maf-definition](maf/kuroe-maf-definition.md) |
| `Kuroe.Maf.Records.Shared` | MAF 流程 | [kuroe-maf-records-shared](maf/kuroe-maf-records-shared.md) |