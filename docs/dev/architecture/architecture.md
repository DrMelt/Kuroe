# Kuroe 总体架构

Kuroe 把用户定义的流程交给模型执行。

本文档给出项目划分与索引，系统结构图见 [design](design/index.md)。

## 项目文档索引

下表按域分组，给出各模块的边界文档。域目录与分组同名。

| 模块 | 域 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Kernel.Execution.Shared` | kernel | [kuroe-kernel-execution-shared](functional_boundary/kernel/kuroe-kernel-execution-shared.md) |
| `Kuroe.Kernel.Execution` | kernel | [kuroe-kernel-execution](functional_boundary/kernel/kuroe-kernel-execution.md) |
| `Kuroe.Kernel.Execution.Records.Shared` | kernel | [kuroe-kernel-execution-records-shared](functional_boundary/kernel/kuroe-kernel-execution-records-shared.md) |
| `Kuroe.Kernel.Input.Shared` | kernel | [kuroe-kernel-input-shared](functional_boundary/kernel/kuroe-kernel-input-shared.md) |
| `Kuroe.Kernel.Input.Runtime` | kernel | [kuroe-kernel-input-runtime](functional_boundary/kernel/kuroe-kernel-input-runtime.md) |
| `Kuroe.Kernel.Control.Shared` | kernel | [kuroe-kernel-control-shared](functional_boundary/kernel/kuroe-kernel-control-shared.md) |
| `Kuroe.Kernel.Control.Runtime` | kernel | [kuroe-kernel-control-runtime](functional_boundary/kernel/kuroe-kernel-control-runtime.md) |
| `Kuroe.Kernel.Tasks.Shared` | kernel | [kuroe-kernel-tasks-shared](functional_boundary/kernel/kuroe-kernel-tasks-shared.md) |
| `Kuroe.Kernel.Tasks.Runtime` | kernel | [kuroe-kernel-tasks-runtime](functional_boundary/kernel/kuroe-kernel-tasks-runtime.md) |
| `Kuroe.Kernel.Tasks.Presentation.Shared` | kernel | [kuroe-kernel-tasks-presentation-shared](functional_boundary/kernel/kuroe-kernel-tasks-presentation-shared.md) |
| `Kuroe.Kernel.Tasks.Records.Shared` | kernel | [kuroe-kernel-tasks-records-shared](functional_boundary/kernel/kuroe-kernel-tasks-records-shared.md) |
| `Kuroe.Kernel.Presentation.Shared` | kernel | [kuroe-kernel-presentation-shared](functional_boundary/kernel/kuroe-kernel-presentation-shared.md) |
| `Kuroe.Kernel.Tasks.Persistence.Files.Shared` | kernel | [kuroe-kernel-tasks-persistence-files-shared](functional_boundary/kernel/kuroe-kernel-tasks-persistence-files-shared.md) |
| `Kuroe.Kernel.Tasks.Persistence.Files` | kernel | [kuroe-kernel-tasks-persistence-files](functional_boundary/kernel/kuroe-kernel-tasks-persistence-files.md) |
| `Kuroe.Kernel.Tasks.Persistence.Saving.Shared` | kernel | [kuroe-kernel-tasks-persistence-saving-shared](functional_boundary/kernel/kuroe-kernel-tasks-persistence-saving-shared.md) |
| `Kuroe.Kernel.Tasks.Persistence.Saving` | kernel | [kuroe-kernel-tasks-persistence-saving](functional_boundary/kernel/kuroe-kernel-tasks-persistence-saving.md) |
| `Kuroe.Kernel.Tasks.Persistence.Restoring.Shared` | kernel | [kuroe-kernel-tasks-persistence-restoring-shared](functional_boundary/kernel/kuroe-kernel-tasks-persistence-restoring-shared.md) |
| `Kuroe.Kernel.Tasks.Persistence.Restoring` | kernel | [kuroe-kernel-tasks-persistence-restoring](functional_boundary/kernel/kuroe-kernel-tasks-persistence-restoring.md) |
| `Kuroe.Kernel.Tasks.Persistence.Serialization.Shared` | kernel | [kuroe-kernel-tasks-persistence-serialization-shared](functional_boundary/kernel/kuroe-kernel-tasks-persistence-serialization-shared.md) |
| `Kuroe.Kernel.Tasks.Persistence.Serialization.Json` | kernel | [kuroe-kernel-tasks-persistence-serialization-json](functional_boundary/kernel/kuroe-kernel-tasks-persistence-serialization-json.md) |
| `Kuroe.Flow.Compilation` | flow | [kuroe-flow-compilation](functional_boundary/flow/kuroe-flow-compilation.md) |
| `Kuroe.Flow.Graph.Shared` | flow | [kuroe-flow-graph-shared](functional_boundary/flow/kuroe-flow-graph-shared.md) |
| `Kuroe.Flow.Records.Shared` | flow | [kuroe-flow-records-shared](functional_boundary/flow/kuroe-flow-records-shared.md) |
| `Kuroe.Flow.NodeType.Container` | flow | [kuroe-flow-nodetype-container](functional_boundary/flow/kuroe-flow-nodetype-container.md) |
| `Kuroe.Flow.Control` | flow | [kuroe-flow-control](functional_boundary/flow/kuroe-flow-control.md) |
| `Kuroe.Flow.Library` | flow | [kuroe-flow-library](functional_boundary/flow/kuroe-flow-library.md) |
| `Kuroe.Flow.Library.Files` | flow | [kuroe-flow-library-files](functional_boundary/flow/kuroe-flow-library-files.md) |
| `Kuroe.Flow.Persistence.Serialization.Graph` | flow | [kuroe-flow-persistence-serialization-graph](functional_boundary/flow/kuroe-flow-persistence-serialization-graph.md) |
| `Kuroe.FlowDescription.Description.Shared` | flow-description | [kuroe-flow-description-description-shared](functional_boundary/flow-description/kuroe-flow-description-description-shared.md) |
| `Kuroe.FlowDescription.Definition.Shared` | flow-description | [kuroe-flow-description-definition-shared](functional_boundary/flow-description/kuroe-flow-description-definition-shared.md) |
| `Kuroe.FlowDescription.Compilation.Shared` | flow-description | [kuroe-flow-description-compilation-shared](functional_boundary/flow-description/kuroe-flow-description-compilation-shared.md) |
| `Kuroe.FlowDescription.Compilation` | flow-description | [kuroe-flow-description-compilation](functional_boundary/flow-description/kuroe-flow-description-compilation.md) |
| `Kuroe.FlowDescription.Json.Shared` | flow-description | [kuroe-flow-description-json-shared](functional_boundary/flow-description/kuroe-flow-description-json-shared.md) |
| `Kuroe.FlowDescription.Json` | flow-description | [kuroe-flow-description-json](functional_boundary/flow-description/kuroe-flow-description-json.md) |
| `Kuroe.CodeFlow.Records.Shared` | codeflow | [kuroe-codeflow-records-shared](functional_boundary/codeflow/kuroe-codeflow-records-shared.md) |
| `Kuroe.CodeFlow.Persistence` | codeflow | [kuroe-codeflow-persistence](functional_boundary/codeflow/kuroe-codeflow-persistence.md) |
| `Kuroe.Maf.Records.Shared` | maf | [kuroe-maf-records-shared](functional_boundary/maf/kuroe-maf-records-shared.md) |
| `Kuroe.Maf.Definition` | maf | [kuroe-maf-definition](functional_boundary/maf/kuroe-maf-definition.md) |
| `Kuroe.Maf.Compilation.Shared` | maf | [kuroe-maf-compilation-shared](functional_boundary/maf/kuroe-maf-compilation-shared.md) |
| `Kuroe.Maf.Compilation` | maf | [kuroe-maf-compilation](functional_boundary/maf/kuroe-maf-compilation.md) |
| `Kuroe.Maf.NodeExecution` | maf | [kuroe-maf-node-execution](functional_boundary/maf/kuroe-maf-node-execution.md) |
| `Kuroe.Maf.Control` | maf | [kuroe-maf-control](functional_boundary/maf/kuroe-maf-control.md) |
| `Kuroe.Maf.Persistence` | maf | [kuroe-maf-persistence](functional_boundary/maf/kuroe-maf-persistence.md) |
| `Kuroe.Maf` | maf | [kuroe-maf](functional_boundary/maf/kuroe-maf.md) |
| `Kuroe.Runner.Shared` | runner | [kuroe-runner-shared](functional_boundary/runner/kuroe-runner-shared.md) |
| `Kuroe.Runner.Llm` | runner | [kuroe-runner-llm](functional_boundary/runner/kuroe-runner-llm.md) |
| `Kuroe.Runner.Command` | runner | [kuroe-runner-command](functional_boundary/runner/kuroe-runner-command.md) |
| `Kuroe.Runner.File` | runner | [kuroe-runner-file](functional_boundary/runner/kuroe-runner-file.md) |
| `Kuroe.Runner.Input` | runner | [kuroe-runner-input](functional_boundary/runner/kuroe-runner-input.md) |
| `Kuroe.Runner.Approval` | runner | [kuroe-runner-approval](functional_boundary/runner/kuroe-runner-approval.md) |
| `Kuroe.Node.Llm` | node | [kuroe-node-llm](functional_boundary/node/kuroe-node-llm.md) |
| `Kuroe.Node.Command` | node | [kuroe-node-command](functional_boundary/node/kuroe-node-command.md) |
| `Kuroe.Node.File` | node | [kuroe-node-file](functional_boundary/node/kuroe-node-file.md) |
| `Kuroe.Node.Input` | node | [kuroe-node-input](functional_boundary/node/kuroe-node-input.md) |
| `Kuroe.Node.Approval` | node | [kuroe-node-approval](functional_boundary/node/kuroe-node-approval.md) |
| `Kuroe.Tool` | tool | [kuroe-tool](functional_boundary/tool/kuroe-tool.md) |
| `Kuroe.Tool.Json` | tool | [kuroe-tool-json](functional_boundary/tool/kuroe-tool-json.md) |
| `Kuroe.Tool.FileAccess.Shared` | tool | [kuroe-tool-fileaccess-shared](functional_boundary/tool/kuroe-tool-fileaccess-shared.md) |
| `Kuroe.Tool.FileAccess` | tool | [kuroe-tool-fileaccess](functional_boundary/tool/kuroe-tool-fileaccess.md) |
| `Kuroe.Tool.Implementation.Command.Shared` | tool | [kuroe-tool-implementation-command-shared](functional_boundary/tool/kuroe-tool-implementation-command-shared.md) |
| `Kuroe.Tool.Implementation.Command` | tool | [kuroe-tool-implementation-command](functional_boundary/tool/kuroe-tool-implementation-command.md) |
| `Kuroe.Tool.Implementation.Command.Json` | tool | [kuroe-tool-implementation-command-json](functional_boundary/tool/kuroe-tool-implementation-command-json.md) |
| `Kuroe.Tool.Implementation.Contract` | tool | [kuroe-tool-implementation-contract](functional_boundary/tool/kuroe-tool-implementation-contract.md) |
| `Kuroe.Tool.Implementation.Dotnet` | tool | [kuroe-tool-implementation-dotnet](functional_boundary/tool/kuroe-tool-implementation-dotnet.md) |
| `Kuroe.Tool.Implementation.Git` | tool | [kuroe-tool-implementation-git](functional_boundary/tool/kuroe-tool-implementation-git.md) |
| `Kuroe.Tool.Implementation.File` | tool | [kuroe-tool-implementation-file](functional_boundary/tool/kuroe-tool-implementation-file.md) |
| `Kuroe.Tool.Implementation.KuroeInfo` | tool | [kuroe-tool-implementation-kuroeinfo](functional_boundary/tool/kuroe-tool-implementation-kuroeinfo.md) |
| `Kuroe.Tool.Implementation.Source` | tool | [kuroe-tool-implementation-source](functional_boundary/tool/kuroe-tool-implementation-source.md) |
| `Kuroe.Tool.Implementation.Time` | tool | [kuroe-tool-implementation-time](functional_boundary/tool/kuroe-tool-implementation-time.md) |
| `Kuroe.Assistant` | assistant | [kuroe-assistant](functional_boundary/assistant/kuroe-assistant.md) |
| `Kuroe.Composition` | host | [kuroe-composition](functional_boundary/host/kuroe-composition.md) |
| `Kuroe.GraphFlow` | host | [kuroe-graph-flow](functional_boundary/host/kuroe-graph-flow.md) |
| `Kuroe.Console` | host | [kuroe-console](functional_boundary/host/kuroe-console.md) |
| `Kuroe.Cli` | host | [kuroe-cli](functional_boundary/host/kuroe-cli.md) |
| `Kuroe.Platform` | platform | [kuroe-platform](functional_boundary/platform/kuroe-platform.md) |
| `Kuroe.Files.Shared` | platform | [kuroe-files-shared](functional_boundary/platform/kuroe-files-shared.md) |
| `Kuroe.Files` | platform | [kuroe-files](functional_boundary/platform/kuroe-files.md) |
| `Kuroe.Process` | platform | [kuroe-process](functional_boundary/platform/kuroe-process.md) |

## 命名空间与路径

命名空间根与项目名一致，程序集内按目录继续分段。`Kuroe.GraphFlow` 分为 `HostInputs` 与 `NodeExecution` 两支。`Kuroe.Runner.Shared` 是唯一例外，命名空间根为 `Kuroe.Runner`。`Kuroe.Flow.Graph.Shared` 分为 `StaticGraph`、`RuntimeGraph`、`Runtime.Nodes` 与 `Graph.Records` 四支，`RuntimeGraph` 之下按执行、节点、端口、就绪与任务分段，`Kuroe.FlowDescription.Description.Shared` 分为项目根与 `Reporting`，`Kuroe.FlowDescription.Definition.Shared` 分为 `Model`、`Flow`、`Nodes`、`Ports` 与 `Validation`，`Kuroe.FlowDescription.Compilation.Shared` 分为项目根、`Flow` 与 `NodeTypes`，`Kuroe.FlowDescription.Compilation` 的命名空间在项目根之下按目录分段，`Kuroe.Flow.Control` 的命名空间全部在 `Kuroe.Flow.Control.Runtime` 之下。

项目名与目录的推导规则见[架构测试](../architecture-tests.md#项目与目录约束)。

引用约束由架构测试固定，见[架构测试](../architecture-tests.md)。
