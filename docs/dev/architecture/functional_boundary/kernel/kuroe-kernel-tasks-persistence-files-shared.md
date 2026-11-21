# Kuroe.Kernel.Tasks.Persistence.Files.Shared

任务存档存储契约：任务级租约、存档持久化端口与读取结果。调用方只登记与注销租约，持有集合由实现维护。

## 职责

- 任务租约：同一任务同一时刻只允许一个进程持有，提供登记与注销。
- 存档持久化端口：按任务键读写任务存档，列出全部存档，读取逐文件报告失败。
- 读取结果：成功读取的存档与逐项失败，失败带存储给出的来源。

## 边界外

- 不含文件实现：存档读写与租约文件句柄在 Kuroe.Kernel.Tasks.Persistence.Files，原子替换在 Kuroe.Files。
- 不含存档文档形状与编解码：在 Kuroe.Kernel.Tasks.Persistence.Serialization.Shared。
- 不含任务域。

## 依赖

- Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Kernel.Tasks.Shared。