# Kuroe.Flow.Hosting

图流程的装配与宿主侧流程库：注册流程库、节点类型、图流程实现与图记录编解码，提供文件存取、导入、宿主开始输入与任务工作目录。

## 职责

- 装配入口：注册流程库、节点类型、图流程实现与图记录编解码，集中给出节点类型与失败策略选项。
- 内置节点定义：随程序登记进节点库，同名处理见[同名覆盖与重名](../../mechanism/overrides.md)。
- 流程库：描述文档是授权来源，编译结果是运行期来源，落盘与读取都经本层。
- 文件存取：节点库与流程文件的路径解析、读取与原子写入。
- 导入：从文件导入节点库或流程，逐条给出说明与不可用流程，同名处理见[同名覆盖与重名](../../mechanism/overrides.md)。
- 宿主开始输入表：按流程的开始端口给出可绑定的输入名，表与流程不一致时在提交阶段报出。
- 任务工作目录：以任务键命名，在启动目录下建立。
- 内置节点使用的执行运行器工厂接线。

## 边界外

- 不含图编译与描述模型：在 Kuroe.Flow.Compilation 与 Kuroe.Document.Shared。
- 不含图运行期与流程控制：在 Kuroe.Flow.Control。
- 不含节点与运行器实现。
- 不含核心装配与入口：在 Kuroe 与宿主项目。

## 依赖

- Kuroe、Kuroe.Shared、Kuroe.Files.Shared、Kuroe.Process、Kuroe.Flow.Graph.Shared、Kuroe.Executor.Shared、Kuroe.Flow.Records.Shared、Kuroe.Flow.Compilation.Shared、Kuroe.Document.Shared、Kuroe.Flow.Compilation、Kuroe.Document.Json.Shared、Kuroe.Document.Json、Kuroe.Input.Shared、Kuroe.Flow.NodeLibrary、Kuroe.Flow.NodeType.Container、Kuroe.Flow.Control、Kuroe.Flow.Persistence.Graph、Kuroe.Node.Llm、Kuroe.Node.File、Kuroe.Node.Command、Kuroe.Node.Input、Kuroe.Node.Approval、Kuroe.Runner.Shared、Kuroe.Runner.Command、Kuroe.Runner.File、Kuroe.Runner.Input、Kuroe.Runner.Approval、Kuroe.Tool、Kuroe.Tool.Json、Kuroe.Tool.Contract、Kuroe.Execution.Shared、Kuroe.Execution.Records.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Presentation.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Control.Shared、Kuroe.Control.Runtime、Kuroe.Tasks.Persistence.Files.Shared、Kuroe.Tasks.Persistence.Files、Kuroe.Presentation.Shared。