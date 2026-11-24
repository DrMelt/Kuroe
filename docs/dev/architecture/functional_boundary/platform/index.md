# 平台

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Platform["路径规则与配置视图<br>Kuroe/Platform"]
    Files["工作区文件<br>Kuroe.Files"]
    Process["进程执行<br>Kuroe.Process"]
```

| 项目 | 节点 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Platform` | 路径规则与配置视图 | [kuroe-platform](platform/kuroe-platform.md) |
| `Kuroe.Files.Shared` | 工作区文件 | [kuroe-files-shared](files/kuroe-files-shared.md) |
| `Kuroe.Files` | 工作区文件 | [kuroe-files](files/kuroe-files.md) |
| `Kuroe.Process` | 进程执行 | [kuroe-process](process/kuroe-process.md) |