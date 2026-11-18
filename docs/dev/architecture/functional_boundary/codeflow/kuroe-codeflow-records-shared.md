# Kuroe.CodeFlow.Records.Shared

代码定义流程的存档记录与种类：流程定义记录与流程状态记录，两项都带实现种类与流程自有的值。

## 职责

- 流程种类常量：代码定义流程的存档种类。
- 流程定义记录：实现种类与流程自有的定义值。
- 流程状态记录：开始状态与流程自有的运行状态值。

## 边界外

- 不含编解码实现：在 Kuroe.CodeFlow.Persistence。
- 不含流程实现：由使用方以代码给出。
- 不依赖执行、图、节点与流程控制。

## 依赖

- Kuroe.Tasks.Shared、Kuroe.Tasks.Records.Shared。