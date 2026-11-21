# Kuroe.Flow.Records.Shared

图流程的存档记录与种类：流程定义记录、流程状态记录、任务开始时的锁定输入，以及流程快照与编译、输入出处与执行步骤标识的两处转换。

## 职责

- 流程种类常量：图驱动流程的存档种类。
- 流程定义记录：流程名、节点库文本与流程文本。
- 流程状态记录：开始快照、节点状态、端口发布历史与调度种子。
- 锁定输入：任务提交时的开始输入与宿主开始端口文本。
- 流程快照来源与编译端口：按流程名捕获当前文档快照，并把快照重新编译为可运行流程，两者由宿主实现。
- 标识转换：图输入出处与执行记录来源标识互转，图节点路径与执行步骤标识互转。

## 边界外

- 不含图结构、编译与流程控制。
- 不含编解码实现。
- 不依赖流程控制与宿主实现。

## 依赖

- Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Flow.Graph.Shared、Kuroe.FlowDescription.Definition.Shared。