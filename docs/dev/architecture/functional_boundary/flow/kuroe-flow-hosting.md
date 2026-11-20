# Kuroe.Flow.Hosting

图流程的装配与宿主侧流程库：注册流程库、节点类型、图流程实现与图记录编解码，提供文件存取、导入、宿主开始输入与任务工作目录。

## 职责

- 装配入口：注册流程库、节点类型、图流程实现与图记录编解码，集中给出节点类型与失败策略选项。
- 内置节点定义：随程序登记进节点库，同名处理见[同名覆盖与重名](../../mechanism/overrides.md)。
- 流程库：流程描述是授权来源，编译结果是运行期来源，落盘与读取都经本层。
- 文件存取：节点库与流程文件的路径解析、读取与原子写入。
- 导入：从文件导入节点库或流程，逐条给出说明与不可用流程，同名处理见[同名覆盖与重名](../../mechanism/overrides.md)。
- 宿主开始输入表：按流程的开始端口给出可绑定的输入名，表与流程不一致时在提交阶段报出。
- 任务工作目录：以任务键命名，在启动目录下建立。
- 描述编译器与图编译器接线。
- 图节点能力注册与内置节点能力接线。

## 边界外

- 不含描述编译与图编译：在 Kuroe.FlowDescription.Compilation 与 Kuroe.Flow.Compilation。
- 不含图运行期与流程控制：在 Kuroe.Flow.Control。
- 不含节点与运行器实现。
- 不含核心装配与入口：在 Kuroe 与宿主项目。

## 依赖

- Kuroe、Kuroe.Shared、Kuroe.Files.Shared、Kuroe.Process、Kuroe.Flow.Compilation、Kuroe.Flow.NodeExecution、Kuroe.Flow.Graph.Shared、Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.FlowDescription.Compilation、Kuroe.Flow.Records.Shared、Kuroe.FlowDescription.Json.Shared、Kuroe.FlowDescription.Json、Kuroe.Kernel.Input.Shared、Kuroe.Flow.NodeType.Container、Kuroe.Flow.Control、Kuroe.Flow.Persistence.Serialization.Graph、Kuroe.Node.Llm、Kuroe.Node.File、Kuroe.Node.Command、Kuroe.Runner.Shared、Kuroe.Runner.Command、Kuroe.Runner.File、Kuroe.Runner.Input、Kuroe.Runner.Approval、Kuroe.Tool、Kuroe.Tool.Json、Kuroe.Tool.Implementation.Contract、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Presentation.Shared、Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Control.Runtime、Kuroe.Kernel.Tasks.Persistence.Files.Shared、Kuroe.Kernel.Tasks.Persistence.Files、Kuroe.Kernel.Presentation.Shared。