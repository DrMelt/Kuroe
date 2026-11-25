# 项目结构

```mermaid
flowchart TD
    LEntry["宿主入口<br>Kuroe.Console<br>Kuroe.Cli"]
    LHost["宿主<br>Kuroe/Host/{Ability,Assistant,Composition,FlowIntegration,Platform}<br>Kuroe/Flow/NodeType"]
    LFlow["流程实现<br>Kuroe/Flow/{Catalog,Compilation,Control,Graph,Library,Persistence,Records}<br>Kuroe/Maf/{Compilation,Control,Definition,Persistence,Records}<br>Kuroe/CodeFlow"]
    LDescription["流程描述<br>Kuroe/FlowDescription"]
    LKernel["内核<br>Kuroe/Kernel"]
    LSystem["系统能力<br>Files<br>Process"]

    LEntry --> LHost
    LEntry --> LFlow
    LEntry --> LDescription
    LEntry --> LKernel
    LHost --> LFlow
    LHost --> LDescription
    LHost --> LKernel
    LHost --> LSystem
    LFlow --> LDescription
    LFlow --> LKernel
    LFlow --> LSystem
    LDescription --> LKernel
    LKernel --> LSystem
```

| 域 | 文件 |
| --- | --- |
| 宿主入口 | [entry](entry/index.md) |
| 宿主 | [host](host/index.md) |
| 流程实现 | [flow](flow/index.md) |
| 流程描述 | [description](description/index.md) |
| 内核 | [kernel](kernel/index.md) |
| 系统能力 | [system](system/index.md) |