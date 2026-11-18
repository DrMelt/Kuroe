# Kuroe.Tasks.Persistence.Restoring

启动加载实现：逐文件读取存档，获取租约后交给运行时恢复任务，失败逐项汇总。

## 职责

- 逐个读取存档文件，读取失败的项记入报告。
- 恢复前获取任务租约，租约不可得的任务跳过并报告。
- 把存档交给运行时重建任务。
- 汇总加载报告。

## 边界外

- 不含加载契约：端口、报告与失败形状在 Kuroe.Tasks.Persistence.Restoring.Shared。
- 不含任务构造与推进循环：在 Kuroe.Tasks.Runtime。
- 不含文件格式与写盘。

## 依赖

- Kuroe.Tasks.Persistence.Restoring.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Tasks.Persistence.Files.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Presentation.Shared。