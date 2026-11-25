# 内核

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Runtime["运行期<br>Kuroe.Kernel/{Control,Input}/Runtime<br>Kuroe.Kernel/Execution/Core<br>Kuroe.Kernel/Tasks/Runtime"]
    Archive["任务存档<br>Kuroe.Kernel/Tasks/Persistence"]
    Contracts["契约<br>Kuroe.Kernel/{Control,Input}/Shared<br>Kuroe.Kernel/Execution/{Shared,Records}<br>Kuroe.Kernel/Presentation/Shared<br>Kuroe.Kernel/Tasks/{Shared,Records,Presentation}"]
    Runtime --> Contracts
    Runtime --> Archive
    Archive --> Contracts
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 运行期 | `Kuroe.Kernel.Execution` | [kuroe-kernel-execution](runtime/kuroe-kernel-execution.md) |
| 运行期 | `Kuroe.Kernel.Control.Runtime` | [kuroe-kernel-control-runtime](runtime/kuroe-kernel-control-runtime.md) |
| 运行期 | `Kuroe.Kernel.Input.Runtime` | [kuroe-kernel-input-runtime](runtime/kuroe-kernel-input-runtime.md) |
| 运行期 | `Kuroe.Kernel.Tasks.Runtime` | [kuroe-kernel-tasks-runtime](runtime/kuroe-kernel-tasks-runtime.md) |
| 任务存档 |  | [archive](archive/index.md) |
| 契约 |  | [contracts](contracts/index.md) |