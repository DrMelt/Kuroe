# 平台

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Platform["路径规则与配置视图<br>Kuroe.Host/Platform"]
    Files["工作区文件<br>Kuroe.Files"]
    Process["进程执行<br>Kuroe.Process"]
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 路径规则与配置视图 | `Kuroe.Host.Platform` | [kuroe-host-platform](platform/kuroe-host-platform.md) |
| 工作区文件 | `Kuroe.Files.Shared` | [kuroe-files-shared](files/kuroe-files-shared.md) |
| 工作区文件 | `Kuroe.Files` | [kuroe-files](files/kuroe-files.md) |
| 进程执行 | `Kuroe.Process` | [kuroe-process](process/kuroe-process.md) |