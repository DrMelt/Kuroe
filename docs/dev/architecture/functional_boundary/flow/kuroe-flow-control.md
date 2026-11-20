# Kuroe.Flow.Control

图流程控制：把编译后的图推进为任务执行，维护运行期节点与端口状态，承担提交、恢复、重新执行与终止，并向宿主提供视图与存档捕获。

## 职责

- 图流程控制实例：推进循环、恢复、按节点重新执行与终止，节点可执行时建立执行。
- 状态绑定：从存档重建图运行期、节点与端口，重新派发中断的执行。
- 提交与恢复：校验开始输入、建立图状态、从存档重建执行上下文。
- 运行状态：锁定的编译结果、开始端口值、运行期节点与执行上下文。
- 图执行适配：装配图侧输入，在中性产出与契约之间转换，不持有执行状态机。
- 节点物化与容器运行期：按流程定义建立运行期节点，容器按作用域组织成员。
- 触发与就绪：按端口触发位与输入进度决定可执行节点。
- 流程表现：从同一份执行状态生成流程视图与等待步骤。
- 存档捕获：流程定义、节点状态与端口发布历史。
- 模板来源与投影：把流程定义与宿主开始输入投影为中性模板。
- 宿主端口：运行期流程模板查询与编译结果查询，执行节点声明解析。

## 边界外

- 不含静态模型与运行期节点形状：在 Kuroe.FlowDescription.Definition.Shared 与 Kuroe.Flow.Graph.Shared。
- 不含编译与库展开：在 Kuroe.FlowDescription.Compilation。
- 不含执行实现：执行状态机在 Kuroe.Kernel.Execution。
- 不含任务域、输入通道与持久化实现。

## 依赖

- Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Flow.Records.Shared、Kuroe.Flow.Graph.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.Kernel.Presentation.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Records.Shared。