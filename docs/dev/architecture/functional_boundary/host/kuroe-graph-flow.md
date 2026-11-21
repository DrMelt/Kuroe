# Kuroe.GraphFlow

图流程的宿主侧装配：在核心装配之后把图流程实现、节点类型、执行器、宿主开始输入与任务目录注册进容器，使核心任务运行时能提交并推进图流程任务。

## 职责

- 图流程装配入口：在核心装配之后注册流程库、节点类型描述、图节点能力、图流程提供者、流程模板与图记录编解码。
- 内置节点定义：随程序登记图流程内置节点，同名覆盖规则与其他节点一致。
- 节点执行装配：把图节点类型绑定到节点项目给出的执行器，并注册输入与审批节点的输出投影。
- 模型声明解析：从图执行节点的静态模型端口取得模型名并校验可用性。
- 宿主开始输入：读取宿主开始输入表，把工作区、任务目录与当前模型解析为开始端口的值。
- 任务工作目录：按任务键建立与删除任务目录。
- 图流程扩展点：允许宿主追加节点类型描述与执行器注册。

## 边界外

- 不含核心装配与任务运行时：在 Kuroe。
- 不含图运行期、图控制与图共享层：在 Kuroe.Flow.Control 与 Kuroe.Flow.Graph.Shared。
- 不含流程库、文件存取与导入：在 Kuroe.Flow.Library。
- 不含节点类型与执行器实现：在 Kuroe.Node.* 与 Kuroe.Runner.*。
- 不含 MAF 流程装配：在 Kuroe.Maf。

## 依赖

- Kuroe、Kuroe.Platform、Kuroe.Files.Shared、Kuroe.Process、Kuroe.Flow.Library、Kuroe.Flow.Compilation、Kuroe.Flow.Control、Kuroe.Flow.Graph.Shared、Kuroe.Flow.Records.Shared、Kuroe.Flow.NodeType.Container、Kuroe.Flow.Persistence.Serialization.Graph、Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.FlowDescription.Compilation、Kuroe.FlowDescription.Json.Shared、Kuroe.FlowDescription.Json、Kuroe.Node.Llm、Kuroe.Node.File、Kuroe.Node.Command、Kuroe.Node.Input、Kuroe.Node.Approval、Kuroe.Runner.Shared、Kuroe.Runner.Command、Kuroe.Runner.File、Kuroe.Runner.Input、Kuroe.Runner.Approval、Kuroe.Tool、Kuroe.Tool.Json、Kuroe.Tool.Implementation.Contract、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Presentation.Shared、Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Control.Runtime、Kuroe.Kernel.Input.Shared、Kuroe.Kernel.Tasks.Persistence.Files.Shared、Kuroe.Kernel.Tasks.Persistence.Files、Kuroe.Kernel.Presentation.Shared。