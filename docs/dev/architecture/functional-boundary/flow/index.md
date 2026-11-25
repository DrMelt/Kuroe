# 流程实现

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    GraphFlow["图流程<br>Kuroe/Flow/{Catalog,Compilation,Control,Graph,Library,Persistence,Records}"]
    MafFlow["MAF 流程<br>Kuroe/Maf/{Compilation,Control,Definition,Persistence,Records}"]
    CodeFlow["代码流程存档<br>Kuroe/CodeFlow"]
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 图流程 |  | [graph](graph/index.md) |
| MAF 流程 |  | [maf](maf/index.md) |
| 代码流程存档 | `Kuroe.CodeFlow.Records.Shared` | [kuroe-code-flow-records-shared](code/kuroe-code-flow-records-shared.md) |
| 代码流程存档 | `Kuroe.CodeFlow.Persistence` | [kuroe-code-flow-persistence](code/kuroe-code-flow-persistence.md) |