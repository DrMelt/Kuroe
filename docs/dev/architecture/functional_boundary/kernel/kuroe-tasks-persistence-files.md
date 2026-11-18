# Kuroe.Tasks.Persistence.Files

任务存档的文件存储实现：每个任务一个文件，写入走原子替换，读取逐文件报告失败，租约按任务键打开独占句柄。

## 职责

- 存档文件读写：按任务键定位文件，列出全部存档。
- 写入原子性：先写临时文件再替换，中断不会留下半份文件，IO 异常交给调用方。
- 任务租约：按任务键打开独占文件句柄，进程退出时由系统释放，锁文件保留。
- 逐文件读取失败报告。

## 边界外

- 不含存储契约：端口、租约与读取结果在 Kuroe.Tasks.Persistence.Files.Shared。
- 不含存档编解码与目录语义：编解码在 Kuroe.Tasks.Persistence.Json，路径与目录由宿主给出。
- 不含任务域与写盘调度。

## 依赖

- Kuroe.Tasks.Persistence.Files.Shared、Kuroe.Tasks.Persistence.Serialization.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Tasks.Shared。