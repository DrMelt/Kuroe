# Kuroe.Flow.Persistence.Serialization.Graph

图流程记录的存档编解码：定义与状态各自成为一段 JSON 片段，字段形状独立于运行期模型。

## 职责

- 图流程定义的存档形状与映射。
- 图流程状态的存档形状与映射：开始快照、节点状态、端口发布与调度种子。
- 节点记录的存档形状：状态、阻塞原因、已消费版本与输出发布。
- 输出发布形状：端口、内容与输入取值出处。
- 片段读写：读取与写出流程片段，约定见[存档与持久化](../../mechanism/persistence.md#编解码边界)。

## 边界外

- 不含流程记录模型与种类：在 Kuroe.Flow.Records.Shared。
- 不含图运行期与流程控制。
- 不含任务存档的中央映射与其他流程种类的编解码。

## 依赖

- Kuroe.Flow.Records.Shared、Kuroe.Flow.Graph.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Records.Shared。