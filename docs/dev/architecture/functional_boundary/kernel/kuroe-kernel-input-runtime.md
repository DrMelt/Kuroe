# Kuroe.Kernel.Input.Runtime

输入通道的进程内实现：按任务与步骤路径登记等待、交付输入、清理登记，变化时通知订阅者。

## 职责

- 等待登记与撤销的进程内存储。
- 一次输入交付到等待中的步骤，交付成功后撤销该登记。
- 任务级登记清理。
- 登记变化通知。

## 边界外

- 不含契约。
- 不含任务域与流程推进。
- 不做跨进程传输。

## 依赖

- Kuroe.Kernel.Input.Shared、Kuroe.Kernel.Tasks.Shared。