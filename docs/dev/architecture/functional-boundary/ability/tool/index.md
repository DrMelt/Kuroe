# 工具

上层结构见[执行能力](../index.md)。

```mermaid
flowchart TD
    Core["核心<br>Kuroe/Tool/{Core,Json}"]
    FileAccess["文件访问<br>Kuroe/Tool/FileAccess"]
    CommandTools["命令工具<br>Kuroe/Tool/Implementation/{Command,Dotnet,Git}"]
    Implementation["工具实现<br>Kuroe/Tool/Implementation/{Contract,File,KuroeInfo,Source,Time}"]
    FileAccess --> Core
    CommandTools --> Core
    Implementation --> Core
    Implementation --> FileAccess
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 核心 | `Kuroe.Tool` | [kuroe-tool](kuroe-tool.md) |
| 核心 | `Kuroe.Tool.Json` | [kuroe-tool-json](kuroe-tool-json.md) |
| 文件访问 | `Kuroe.Tool.FileAccess.Shared` | [kuroe-tool-file-access-shared](kuroe-tool-file-access-shared.md) |
| 文件访问 | `Kuroe.Tool.FileAccess` | [kuroe-tool-file-access](kuroe-tool-file-access.md) |
| 命令工具 | `Kuroe.Tool.Implementation.Command.Shared` | [kuroe-tool-implementation-command-shared](kuroe-tool-implementation-command-shared.md) |
| 命令工具 | `Kuroe.Tool.Implementation.Command` | [kuroe-tool-implementation-command](kuroe-tool-implementation-command.md) |
| 命令工具 | `Kuroe.Tool.Implementation.Command.Json` | [kuroe-tool-implementation-command-json](kuroe-tool-implementation-command-json.md) |
| 命令工具 | `Kuroe.Tool.Implementation.Dotnet` | [kuroe-tool-implementation-dotnet](kuroe-tool-implementation-dotnet.md) |
| 命令工具 | `Kuroe.Tool.Implementation.Git` | [kuroe-tool-implementation-git](kuroe-tool-implementation-git.md) |
| 工具实现 | `Kuroe.Tool.Implementation.Contract` | [kuroe-tool-implementation-contract](kuroe-tool-implementation-contract.md) |
| 工具实现 | `Kuroe.Tool.Implementation.File` | [kuroe-tool-implementation-file](kuroe-tool-implementation-file.md) |
| 工具实现 | `Kuroe.Tool.Implementation.KuroeInfo` | [kuroe-tool-implementation-kuroe-info](kuroe-tool-implementation-kuroe-info.md) |
| 工具实现 | `Kuroe.Tool.Implementation.Source` | [kuroe-tool-implementation-source](kuroe-tool-implementation-source.md) |
| 工具实现 | `Kuroe.Tool.Implementation.Time` | [kuroe-tool-implementation-time](kuroe-tool-implementation-time.md) |