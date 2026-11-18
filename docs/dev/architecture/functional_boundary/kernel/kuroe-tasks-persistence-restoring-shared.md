# Kuroe.Tasks.Persistence.Restoring.Shared

启动加载契约：读取存档、按存档恢复任务并汇总报告。任务的构造、加入任务域与推进循环恢复由运行时实现。

## 职责

- 启动加载端口：读取全部存档并逐个恢复任务。
- 恢复运行时端口：把一份存档交给运行时重建任务。
- 加载报告：成功恢复数量与逐项失败。
- 加载失败：带任务键或文件级原因，文件级失败没有任务键。

## 边界外

- 不含加载实现：逐文件读取与租约获取在 Kuroe.Tasks.Persistence.Restoring。
- 不含任务构造与任务域：在 Kuroe.Tasks.Runtime。
- 不含编解码与文件存储。

## 依赖

- Kuroe.Tasks.Records.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Presentation.Shared。