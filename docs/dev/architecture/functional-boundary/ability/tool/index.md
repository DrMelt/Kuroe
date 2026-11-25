# 工具

上层结构见[执行能力](../index.md)。

```mermaid
flowchart TD
    Core["工具面与工具文件<br>Kuroe/Ability/Tool/{Core,Json}"]
    FileAccess["文件访问<br>Kuroe/Ability/Tool/FileAccess"]
    CommandTools["命令工具<br>Kuroe/Ability/Tool/Implementation/{Command,Dotnet,Git}"]
    Implementation["工具实现<br>Kuroe/Ability/Tool/Implementation/{Contract,File,KuroeInfo,Source,Time}"]
    FileAccess --> Core
    CommandTools --> Core
    Implementation --> Core
    Implementation --> FileAccess
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 工具面与工具文件 | `Kuroe.Ability.Tool` | [kuroe-ability-tool](kuroe-ability-tool.md) |
| 工具面与工具文件 | `Kuroe.Ability.Tool.Json` | [kuroe-ability-tool-json](kuroe-ability-tool-json.md) |
| 文件访问 | `Kuroe.Ability.Tool.FileAccess.Shared` | [kuroe-ability-tool-file-access-shared](kuroe-ability-tool-file-access-shared.md) |
| 文件访问 | `Kuroe.Ability.Tool.FileAccess` | [kuroe-ability-tool-file-access](kuroe-ability-tool-file-access.md) |
| 命令工具 | `Kuroe.Ability.Tool.Implementation.Command.Shared` | [kuroe-ability-tool-implementation-command-shared](kuroe-ability-tool-implementation-command-shared.md) |
| 命令工具 | `Kuroe.Ability.Tool.Implementation.Command` | [kuroe-ability-tool-implementation-command](kuroe-ability-tool-implementation-command.md) |
| 命令工具 | `Kuroe.Ability.Tool.Implementation.Command.Json` | [kuroe-ability-tool-implementation-command-json](kuroe-ability-tool-implementation-command-json.md) |
| 命令工具 | `Kuroe.Ability.Tool.Implementation.Dotnet` | [kuroe-ability-tool-implementation-dotnet](kuroe-ability-tool-implementation-dotnet.md) |
| 命令工具 | `Kuroe.Ability.Tool.Implementation.Git` | [kuroe-ability-tool-implementation-git](kuroe-ability-tool-implementation-git.md) |
| 工具实现 | `Kuroe.Ability.Tool.Implementation.Contract` | [kuroe-ability-tool-implementation-contract](kuroe-ability-tool-implementation-contract.md) |
| 工具实现 | `Kuroe.Ability.Tool.Implementation.File` | [kuroe-ability-tool-implementation-file](kuroe-ability-tool-implementation-file.md) |
| 工具实现 | `Kuroe.Ability.Tool.Implementation.KuroeInfo` | [kuroe-ability-tool-implementation-kuroe-info](kuroe-ability-tool-implementation-kuroe-info.md) |
| 工具实现 | `Kuroe.Ability.Tool.Implementation.Source` | [kuroe-ability-tool-implementation-source](kuroe-ability-tool-implementation-source.md) |
| 工具实现 | `Kuroe.Ability.Tool.Implementation.Time` | [kuroe-ability-tool-implementation-time](kuroe-ability-tool-implementation-time.md) |