# Kuroe.Flow.NodeExecution

图流程内置节点的执行装配：把节点类型的中立执行器绑定注册为图能力，并提供图流程专属的输出投影与模型声明解析。

## 职责

- 核心执行器装配：按节点类型名注册 Llm、Command、FileRead 与 FileWrite 的执行器绑定。
- 输出投影：输入与审批节点的结果按图发布形状投影。
- 模型声明解析：从图执行节点取得模型端口值并交给执行器。
- 交互节点注册：输入与审批节点的执行器与输出投影单独注册，供宿主按需要追加。
- 步骤路径转换：把执行步骤标识还原为图节点路径。

## 边界外

- 不含节点类型、参数与校验：在 Kuroe.Node.*。
- 不含图运行期与控制：在 Kuroe.Flow.Control。
- 不含图编译与描述构造：在 Kuroe.Flow.Compilation 与 Kuroe.FlowDescription.*。
- 不含 MAF 步骤装配与注册：在 Kuroe.Maf.NodeExecution。

## 依赖

- Kuroe.Node.Llm、Kuroe.Node.File、Kuroe.Node.Command、Kuroe.Node.Input、Kuroe.Node.Approval、Kuroe.Flow.Graph.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Runner.Shared、Kuroe.Runner.Input、Kuroe.Runner.Approval。