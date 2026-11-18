# Kuroe.Flow.Persistence.Graph

图流程记录的存档编解码：定义与状态各自成为一段 JSON 片段，字段形状独立于运行期模型。

## 职责

- 图流程定义的存档形状与映射。
- 图流程状态的存档形状与映射：开始快照、节点状态、端口发布与调度种子。
- 节点记录的存档形状：状态、阻塞原因、已消费版本与输出发布。
- 输出发布形状：端口、内容与输入取值出处。
- 片段读写约定：读取拒绝未定义字段，写出省略空字段。

## 边界外

- 不含流程记录模型与种类：在 Kuroe.Flow.Records.Shared。
- 不含图运行期与流程控制。
- 不含任务存档的中央映射与其他流程种类的编解码。

## 依赖

- Kuroe.Flow.Records.Shared、Kuroe.Flow.Node.Shared、Kuroe.Execution.Shared、Kuroe.Execution.Records.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Records.Shared。