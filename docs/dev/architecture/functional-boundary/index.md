# 项目结构

```mermaid
flowchart TD
    LEntry["宿主入口<br>Kuroe.Console<br>Kuroe.Cli"]
    LAssembly["宿主装配<br>Kuroe/{Composition,Assistant,FlowIntegration}"]
    LFlow["流程实现<br>Kuroe.Flow/{Catalog,Compilation,Control,Graph,Library,Persistence,Records}<br>Kuroe.Maf/{Compilation,Control,Definition,Persistence,Records}<br>Kuroe.CodeFlow"]
    LDescription["流程描述<br>Kuroe.FlowDescription"]
    LAbility["执行能力<br>Kuroe/Ability<br>Kuroe.Flow/NodeType"]
    LKernel["内核<br>Kuroe.Kernel"]
    LPlatform["平台<br>Kuroe/Platform<br>Kuroe.Files<br>Kuroe.Process"]

    LEntry --> LAssembly
    LEntry --> LFlow
    LEntry --> LDescription
    LEntry --> LAbility
    LEntry --> LKernel
    LEntry --> LPlatform
    LAssembly --> LFlow
    LAssembly --> LDescription
    LAssembly --> LAbility
    LAssembly --> LKernel
    LAssembly --> LPlatform
    LFlow --> LDescription
    LFlow --> LKernel
    LFlow --> LPlatform
    LDescription --> LKernel
    LAbility --> LDescription
    LAbility --> LKernel
    LAbility --> LPlatform
    LKernel --> LPlatform
```

| 域 | 文件 |
| --- | --- |
| 宿主入口 | [entry](entry/index.md) |
| 宿主装配 | [assembly](assembly/index.md) |
| 流程实现 | [flow](flow/index.md) |
| 流程描述 | [description](description/index.md) |
| 执行能力 | [ability](ability/index.md) |
| 内核 | [kernel](kernel/index.md) |
| 平台 | [platform](platform/index.md) |