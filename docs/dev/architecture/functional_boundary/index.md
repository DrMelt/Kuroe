# 项目结构

```mermaid
flowchart TD
    LEntry["入口<br>Kuroe.Console<br>Kuroe.Cli"]
    LAssembly["装配<br>Kuroe/{Composition,GraphFlow,Assistant}<br>Kuroe.Maf/{Core,NodeExecution}"]
    LFlow["流程实现<br>Kuroe.Flow/{Control,Graph,Records}<br>Kuroe.Maf/{Control,Definition,Records}"]
    LDescription["描述编译<br>Kuroe.FlowDescription<br>Kuroe.Flow/{Compilation,Library}<br>Kuroe.Maf/Compilation"]
    LAbility["执行能力<br>Kuroe/{Node,Runner,Tool}<br>Kuroe.Flow/NodeType"]
    LPersistence["存档恢复<br>Kuroe.Kernel/Tasks/Persistence<br>Kuroe.Flow/Persistence<br>Kuroe.CodeFlow<br>Kuroe.Maf/Persistence"]
    LKernel["内核<br>Kuroe.Kernel/{Control,Input}/{Runtime,Shared}<br>Kuroe.Kernel/Execution/{Core,Shared,Records}<br>Kuroe.Kernel/Tasks/{Runtime,Shared,Records,Presentation}<br>Kuroe.Kernel/Presentation"]
    LPlatform["平台<br>Kuroe/Platform<br>Kuroe.Files<br>Kuroe.Process"]

    LEntry --> LAssembly
    LEntry --> LFlow
    LEntry --> LDescription
    LEntry --> LAbility
    LEntry --> LKernel
    LEntry --> LPersistence
    LEntry --> LPlatform
    LAssembly --> LFlow
    LAssembly --> LDescription
    LAssembly --> LAbility
    LAssembly --> LKernel
    LAssembly --> LPersistence
    LAssembly --> LPlatform
    LFlow --> LDescription
    LFlow --> LKernel
    LDescription --> LFlow
    LDescription --> LKernel
    LDescription --> LPlatform
    LAbility --> LFlow
    LAbility --> LDescription
    LAbility --> LKernel
    LAbility --> LPlatform
    LPersistence --> LFlow
    LPersistence --> LDescription
    LPersistence --> LKernel
    LPersistence --> LPlatform
    LKernel --> LPersistence
```

| 层 | 文件 |
| --- | --- |
| 入口 | [entry](entry/index.md) |
| 装配 | [assembly](assembly/index.md) |
| 流程实现 | [flow](flow/index.md) |
| 描述编译 | [description](description/index.md) |
| 执行能力 | [ability](ability/index.md) |
| 存档恢复 | [persistence](persistence/index.md) |
| 内核 | [kernel](kernel/index.md) |
| 平台 | [platform](platform/index.md) |