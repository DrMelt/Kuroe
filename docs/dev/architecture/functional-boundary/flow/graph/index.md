# 图流程

上层结构见[流程实现](../index.md)。

```mermaid
flowchart TD
    Records["记录<br>Kuroe.Flow/Records"]
    GraphModel["模型<br>Kuroe.Flow/Graph"]
    Compilation["编译<br>Kuroe.Flow/Compilation"]
    Control["控制<br>Kuroe.Flow/Control"]
    Library["流程库<br>Kuroe.Flow/Library"]
    Persistence["持久化<br>Kuroe.Flow/Persistence"]
    Records --> GraphModel
    Compilation --> GraphModel
    Control --> Records
    Control --> GraphModel
    Library --> Compilation
    Library --> Control
    Library --> GraphModel
    Library --> Records
    Persistence --> Records
    Persistence --> GraphModel
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 记录 | `Kuroe.Flow.Records.Shared` | [kuroe-flow-records-shared](kuroe-flow-records-shared.md) |
| 模型 | `Kuroe.Flow.Graph.Shared` | [kuroe-flow-graph-shared](kuroe-flow-graph-shared.md) |
| 编译 | `Kuroe.Flow.Compilation` | [kuroe-flow-compilation](kuroe-flow-compilation.md) |
| 控制 | `Kuroe.Flow.Control` | [kuroe-flow-control](kuroe-flow-control.md) |
| 流程库 | `Kuroe.Flow.Library` | [kuroe-flow-library](kuroe-flow-library.md) |
| 流程库 | `Kuroe.Flow.Library.Files` | [kuroe-flow-library-files](kuroe-flow-library-files.md) |
| 持久化 | `Kuroe.Flow.Persistence.Serialization.Graph` | [kuroe-flow-persistence-serialization-graph](kuroe-flow-persistence-serialization-graph.md) |