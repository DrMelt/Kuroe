# Kuroe.Flow.Library

流程库：把节点库与流程文件读成流程描述集合并编译为运行期流程，提供文件存取、导入与流程快照来源。不依赖宿主装配、节点与执行器。

## 职责

- 流程库：流程描述是授权来源，编译结果是运行期来源，逐条流程隔离编译失败。
- 流程快照：按流程文件或流程文本重建可运行流程集合与不可用流程清单。
- 流程文件存取：节点库与流程文件的路径解析、读取与原子写入。
- 导入：从文件导入节点库或流程，给出导入条目与逐条说明。
- 流程编译接线：把描述编译器与图编译器组合为流程编译入口。

## 边界外

- 不含宿主装配、宿主开始输入与任务目录：在 Kuroe.GraphFlow。
- 不含图运行期与图控制：在 Kuroe.Flow.Control。
- 不含描述模型与描述编译：在 Kuroe.FlowDescription.*。
- 不含节点类型与执行器实现：在 Kuroe.Node.* 与 Kuroe.Runner.*。

## 依赖

- Kuroe.Flow.Compilation、Kuroe.Flow.Control、Kuroe.Flow.Graph.Shared、Kuroe.Flow.Records.Shared、Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.FlowDescription.Json.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Files.Shared。