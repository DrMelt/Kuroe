# Kuroe 总体架构

Kuroe 把用户定义的流程交给模型执行。内核提供任务、执行与流程控制的中性机制，流程实现按图、代码与 MAF 三种形态各自落位，节点与工具把执行接到具体能力上，宿主负责装配。

流程描述文档契约位于 `Source/Kuroe.Document`，由图流程与 MAF 流程共同使用。

本文档给出项目划分与依赖关系。

## 解决方案划分

`A --> B` 表示 A 依赖 B，边与实际项目引用一一对应。分组维度是域，与项目名前缀无关：`Kuroe.Tool.*` 归 tool，`Kuroe.Tasks.*` 归 kernel，`Kuroe.Flow.*` 归 flow。

Kernel 只提供中性机制，不出现图与节点词汇，不定义具体流程种类。流程种类常量与流程记录由流程实现拥有。

```mermaid
graph TD
    subgraph DKernel["kernel：中性契约、任务域、执行与流程控制"]
        ExecutionShared["Execution.Shared<br>执行身份 / 请求 / 契约 / 产出"]
        Execution["Execution<br>执行状态机与派发"]
        ExecutionRecordsShared["Execution.Records.Shared<br>执行存档记录"]
        InputShared["Input.Shared<br>输入登记与交付"]
        InputRuntime["Input.Runtime<br>输入通道实现"]
        ControlShared["Control.Shared<br>流程控制契约与任务控制面"]
        ControlRuntime["Control.Runtime<br>流程运行期与执行网关"]
        TasksShared["Tasks.Shared<br>任务标识 / 流程身份 / 端口"]
        TasksRuntime["Tasks.Runtime<br>任务域与任务注册表"]
        TasksPresentationShared["Tasks.Presentation.Shared<br>任务快照与等待步骤"]
        TasksRecordsShared["Tasks.Records.Shared<br>任务与流程存档记录"]
        PresentationShared["Presentation.Shared<br>流程视图与展示文本"]
        TasksPersistenceFilesShared["Tasks.Persistence.Files.Shared<br>存档文件与租约契约"]
        TasksPersistenceFiles["Tasks.Persistence.Files<br>存档文件存储与租约"]
        TasksPersistenceSavingShared["Tasks.Persistence.Saving.Shared<br>写盘协调契约"]
        TasksPersistenceSaving["Tasks.Persistence.Saving<br>写盘协调"]
        TasksPersistenceRestoringShared["Tasks.Persistence.Restoring.Shared<br>启动恢复契约"]
        TasksPersistenceRestoring["Tasks.Persistence.Restoring<br>启动恢复服务"]
        TasksPersistenceSerializationShared["Tasks.Persistence.Serialization.Shared<br>存档文档与映射"]
        TasksPersistenceJson["Tasks.Persistence.Json<br>存档 JSON 编解码"]
    end

    subgraph DFlow["flow：图流程模型、编译、控制与装配"]
        FlowGraphShared["Flow.Graph.Shared<br>标识模型 / 图结构 / 运行期节点 / 节点规则"]
        FlowRecordsShared["Flow.Records.Shared<br>图流程记录与种类"]
        FlowCompilationShared["Flow.Compilation.Shared<br>编译契约 / 类型描述 / 参数读侧"]
        FlowCompilation["Flow.Compilation<br>流程描述编译与图编译"]
        FlowNodeLibrary["Flow.NodeLibrary<br>节点库展开与作用域"]
        FlowNodeTypeContainer["Flow.NodeType.Container<br>容器节点类型"]
        FlowControl["Flow.Control<br>图流程控制与运行期"]
        FlowHosting["Flow.Hosting<br>图流程注册与流程库装配"]
        FlowPersistenceGraph["Flow.Persistence.Graph<br>图流程记录编解码"]
    end

    subgraph DDocument["document：流程描述文档契约"]
        DocumentShared["Document.Shared<br>流程描述 / 参数段"]
        DocumentJsonShared["Document.Json.Shared<br>流程文件格式端口"]
        DocumentJson["Document.Json<br>流程文件 JSON 读写"]
    end

    subgraph DCodeFlow["codeflow：代码流程"]
        CodeFlowRecordsShared["CodeFlow.Records.Shared<br>代码流程记录与种类"]
        CodeFlowPersistence["CodeFlow.Persistence<br>代码流程记录编解码"]
    end

    subgraph DMaf["maf：Microsoft Agent Framework 流程"]
        MafRecordsShared["Maf.Records.Shared<br>MAF 流程记录与种类"]
        MafDefinition["Maf.Definition<br>MAF 流程定义与步骤"]
        MafCompilationShared["Maf.Compilation.Shared<br>文档装配契约"]
        MafCompilation["Maf.Compilation<br>文档装配与投影"]
        MafControl["Maf.Control<br>MAF 流程控制与步骤桥接"]
        MafPersistence["Maf.Persistence<br>MAF 流程记录编解码"]
        Maf["Maf<br>MAF 流程装配入口"]
    end

    subgraph DRunner["runner：执行运行器"]
        RunnerShared["Runner.Shared<br>模型接入契约与模型错误"]
        RunnerLlm["Runner.Llm<br>Llm 运行器与会话"]
        RunnerCommand["Runner.Command<br>命令运行器"]
        RunnerFile["Runner.File<br>文件读写运行器"]
        RunnerInput["Runner.Input<br>输入等待运行器"]
        RunnerApproval["Runner.Approval<br>审批等待运行器"]
    end

    subgraph DNode["node：节点类型"]
        NodeLlm["Node.Llm<br>Llm 节点类型"]
        NodeCommand["Node.Command<br>命令节点类型"]
        NodeFile["Node.File<br>文件节点类型"]
        NodeInput["Node.Input<br>输入节点类型"]
        NodeApproval["Node.Approval<br>审批节点类型"]
    end

    subgraph DTool["tool：工具载体与工具组"]
        Tool["Tool<br>工具载体与工具面"]
        ToolJson["Tool.Json<br>工具文件注册"]
        ToolFileAccessShared["Tool.FileAccess.Shared<br>文件访问契约"]
        ToolFileAccess["Tool.FileAccess<br>文件访问实现"]
        ToolCommandShared["Tool.Command.Shared<br>命令模板工具契约"]
        ToolCommand["Tool.Command<br>命令模板工具执行"]
        ToolCommandJson["Tool.Command.Json<br>命令工具文件读写"]
        ToolContract["Tool.Contract<br>执行契约工具"]
        ToolDotnet["Tool.Dotnet<br>dotnet 工具组"]
        ToolGit["Tool.Git<br>git 工具组"]
        ToolFile["Tool.File<br>file 工具组"]
        ToolKuroeInfo["Tool.KuroeInfo<br>KuroeInfo 工具组"]
        ToolSource["Tool.Source<br>source 工具组"]
        ToolTime["Tool.Time<br>时间工具"]
    end

    subgraph DAssistant["assistant：内置助手"]
        Assistant["Assistant<br>会话任务域与助手工具装配"]
    end

    subgraph DHost["host：宿主装配与入口"]
        Core["Kuroe<br>核心装配与任务域组合"]
        Console["Console<br>交互终端入口"]
        Cli["Cli<br>命令行入口"]
    end

    subgraph DPlatform["platform：路径、文件与进程"]
        Shared["Shared<br>路径变量与配置视图契约"]
        FilesShared["Files.Shared<br>工作区文件契约"]
        Files["Files<br>工作区文件实现"]
        Process["Process<br>进程执行端口与实现"]
    end

    %% kernel 域：中性机制自下而上，持久化实现只依赖共享契约
    ControlRuntime --> ControlShared
    ControlRuntime --> ExecutionShared
    ControlRuntime --> TasksShared
    ControlRuntime --> TasksPresentationShared
    ControlRuntime --> PresentationShared
    ControlShared --> TasksShared
    ControlShared --> ExecutionShared
    ControlShared --> PresentationShared
    ControlShared --> TasksPresentationShared
    ControlShared --> TasksRecordsShared
    Execution --> ExecutionShared
    ExecutionRecordsShared --> ExecutionShared
    InputRuntime --> InputShared
    InputRuntime --> TasksShared
    InputShared --> TasksShared
    PresentationShared --> ExecutionShared
    PresentationShared --> TasksShared
    TasksPersistenceFiles --> TasksPersistenceFilesShared
    TasksPersistenceFiles --> TasksPersistenceSerializationShared
    TasksPersistenceFiles --> TasksRecordsShared
    TasksPersistenceFiles --> TasksShared
    TasksPersistenceFilesShared --> TasksRecordsShared
    TasksPersistenceFilesShared --> TasksShared
    TasksPersistenceRestoring --> TasksPersistenceRestoringShared
    TasksPersistenceRestoring --> TasksRecordsShared
    TasksPersistenceRestoring --> TasksPersistenceFilesShared
    TasksPersistenceRestoring --> TasksShared
    TasksPersistenceRestoring --> TasksPresentationShared
    TasksPersistenceRestoringShared --> TasksRecordsShared
    TasksPersistenceRestoringShared --> TasksShared
    TasksPersistenceRestoringShared --> TasksPresentationShared
    TasksPersistenceSaving --> TasksPersistenceSavingShared
    TasksPersistenceSaving --> TasksRecordsShared
    TasksPersistenceSaving --> TasksPersistenceFilesShared
    TasksPersistenceSaving --> TasksShared
    TasksPersistenceSaving --> TasksPresentationShared
    TasksPersistenceSavingShared --> TasksRecordsShared
    TasksPersistenceSavingShared --> TasksPresentationShared
    TasksPersistenceSavingShared --> TasksShared
    TasksPersistenceJson --> TasksPersistenceSerializationShared
    TasksPersistenceJson --> ExecutionShared
    TasksPersistenceJson --> TasksShared
    TasksPersistenceJson --> TasksRecordsShared
    TasksPersistenceSerializationShared --> ExecutionShared
    TasksPersistenceSerializationShared --> ExecutionRecordsShared
    TasksPersistenceSerializationShared --> TasksRecordsShared
    TasksPersistenceSerializationShared --> TasksShared
    TasksPresentationShared --> ExecutionShared
    TasksPresentationShared --> PresentationShared
    TasksPresentationShared --> TasksShared
    TasksRecordsShared --> TasksShared
    TasksRecordsShared --> ExecutionRecordsShared
    TasksRecordsShared --> ExecutionShared
    TasksRuntime --> ExecutionShared
    TasksRuntime --> ExecutionRecordsShared
    TasksRuntime --> TasksShared
    TasksRuntime --> TasksPresentationShared
    TasksRuntime --> TasksRecordsShared
    TasksRuntime --> PresentationShared
    TasksRuntime --> ControlShared
    TasksRuntime --> InputShared
    TasksRuntime --> TasksPersistenceFilesShared
    TasksRuntime --> TasksPersistenceSavingShared
    TasksRuntime --> TasksPersistenceRestoringShared

    %% flow 域：模型、编译、控制与装配分层，装配根向下接节点与运行器
    FlowCompilation --> FlowCompilationShared
    FlowCompilation --> DocumentShared
    FlowCompilation --> TasksShared
    FlowCompilation --> FlowGraphShared
    DocumentJson --> DocumentJsonShared
    DocumentJson --> DocumentShared
    DocumentJson --> TasksShared
    DocumentJson --> FlowGraphShared
    DocumentJsonShared --> DocumentShared
    FlowCompilationShared --> DocumentShared
    FlowCompilationShared --> TasksShared
    FlowCompilationShared --> FlowGraphShared
    FlowControl --> ControlShared
    FlowControl --> ExecutionShared
    FlowControl --> ExecutionRecordsShared
    FlowControl --> FlowRecordsShared
    FlowControl --> FlowGraphShared
    FlowControl --> PresentationShared
    FlowControl --> TasksShared
    FlowControl --> TasksRecordsShared
    FlowRecordsShared --> TasksShared
    FlowRecordsShared --> TasksRecordsShared
    FlowRecordsShared --> ExecutionShared
    FlowRecordsShared --> FlowGraphShared
    FlowHosting --> Core
    FlowHosting --> Shared
    FlowHosting --> FilesShared
    FlowHosting --> Process
    FlowHosting --> FlowGraphShared
    FlowHosting --> FlowRecordsShared
    FlowHosting --> FlowCompilationShared
    FlowHosting --> DocumentShared
    FlowHosting --> FlowCompilation
    FlowHosting --> DocumentJsonShared
    FlowHosting --> DocumentJson
    FlowHosting --> InputShared
    FlowHosting --> FlowNodeLibrary
    FlowHosting --> FlowNodeTypeContainer
    FlowHosting --> FlowControl
    FlowHosting --> FlowPersistenceGraph
    FlowHosting --> NodeLlm
    FlowHosting --> NodeFile
    FlowHosting --> NodeCommand
    FlowHosting --> NodeInput
    FlowHosting --> NodeApproval
    FlowHosting --> RunnerShared
    FlowHosting --> RunnerCommand
    FlowHosting --> RunnerFile
    FlowHosting --> RunnerInput
    FlowHosting --> RunnerApproval
    FlowHosting --> Tool
    FlowHosting --> ToolJson
    FlowHosting --> ToolContract
    FlowHosting --> ExecutionShared
    FlowHosting --> ExecutionRecordsShared
    FlowHosting --> TasksShared
    FlowHosting --> TasksPresentationShared
    FlowHosting --> TasksRecordsShared
    FlowHosting --> ControlShared
    FlowHosting --> ControlRuntime
    FlowHosting --> TasksPersistenceFilesShared
    FlowHosting --> TasksPersistenceFiles
    FlowHosting --> PresentationShared

    %% document 域：流程文件与流程实现共用的描述文档
    DocumentShared --> FlowGraphShared
    MafCompilationShared --> DocumentShared
    MafCompilation --> DocumentShared

    FlowGraphShared --> TasksShared
    FlowGraphShared --> ExecutionShared
    FlowNodeLibrary --> FlowCompilationShared
    FlowNodeLibrary --> TasksShared
    FlowNodeLibrary --> FlowGraphShared
    FlowNodeTypeContainer --> FlowCompilationShared
    FlowNodeTypeContainer --> TasksShared
    FlowNodeTypeContainer --> FlowGraphShared
    FlowPersistenceGraph --> FlowRecordsShared
    FlowPersistenceGraph --> FlowGraphShared
    FlowPersistenceGraph --> ExecutionShared
    FlowPersistenceGraph --> ExecutionRecordsShared
    FlowPersistenceGraph --> TasksShared
    FlowPersistenceGraph --> TasksRecordsShared

    %% codeflow 域对 flow 无引用；maf 运行期对 flow 无引用，文档装配只引用共享契约
    CodeFlowPersistence --> CodeFlowRecordsShared
    CodeFlowPersistence --> TasksShared
    CodeFlowPersistence --> TasksRecordsShared
    CodeFlowRecordsShared --> TasksShared
    CodeFlowRecordsShared --> TasksRecordsShared
    MafControl --> ControlShared
    MafControl --> ExecutionShared
    MafControl --> ExecutionRecordsShared
    MafControl --> PresentationShared
    MafControl --> TasksShared
    MafControl --> TasksRecordsShared
    MafControl --> MafRecordsShared
    MafControl --> MafDefinition
    MafCompilationShared --> MafDefinition
    MafCompilationShared --> FlowGraphShared
    MafCompilationShared --> TasksShared
    MafCompilation --> MafCompilationShared
    MafCompilation --> MafDefinition
    MafCompilation --> FlowGraphShared
    MafCompilation --> FlowCompilationShared
    MafCompilation --> TasksShared
    MafCompilation --> ExecutionShared
    Maf --> ControlShared
    Maf --> TasksRecordsShared
    Maf --> MafDefinition
    Maf --> MafControl
    Maf --> MafPersistence
    MafDefinition --> TasksShared
    MafDefinition --> ExecutionShared
    MafPersistence --> TasksShared
    MafPersistence --> TasksRecordsShared
    MafPersistence --> MafRecordsShared
    MafRecordsShared --> TasksShared
    MafRecordsShared --> TasksRecordsShared

    %% runner 与 node 域：节点引用自身运行器，运行器不引用节点与图
    RunnerApproval --> ExecutionShared
    RunnerApproval --> InputShared
    RunnerApproval --> TasksShared
    RunnerCommand --> ExecutionShared
    RunnerCommand --> Process
    RunnerCommand --> Shared
    RunnerFile --> ExecutionShared
    RunnerFile --> FilesShared
    RunnerFile --> Shared
    RunnerInput --> ExecutionShared
    RunnerInput --> InputShared
    RunnerInput --> TasksShared
    RunnerLlm --> RunnerShared
    RunnerLlm --> ExecutionShared
    RunnerLlm --> Tool
    NodeApproval --> FlowCompilationShared
    NodeApproval --> RunnerApproval
    NodeApproval --> TasksShared
    NodeApproval --> FlowGraphShared
    NodeApproval --> ExecutionShared
    NodeCommand --> FlowCompilationShared
    NodeCommand --> Shared
    NodeCommand --> RunnerCommand
    NodeCommand --> TasksShared
    NodeCommand --> FlowGraphShared
    NodeCommand --> ExecutionShared
    NodeFile --> FlowCompilationShared
    NodeFile --> Shared
    NodeFile --> RunnerFile
    NodeFile --> TasksShared
    NodeFile --> FlowGraphShared
    NodeFile --> ExecutionShared
    NodeInput --> FlowCompilationShared
    NodeInput --> RunnerInput
    NodeInput --> TasksShared
    NodeInput --> FlowGraphShared
    NodeInput --> ExecutionShared
    NodeLlm --> Tool
    NodeLlm --> RunnerShared
    NodeLlm --> RunnerLlm
    NodeLlm --> FlowCompilationShared
    NodeLlm --> Shared
    NodeLlm --> TasksShared
    NodeLlm --> FlowGraphShared
    NodeLlm --> ExecutionShared

    %% tool 域：载体在最上，工具组按下层能力组装
    Tool --> Shared
    Tool --> ExecutionShared
    Tool --> TasksShared
    ToolFileAccess --> ToolFileAccessShared
    ToolFileAccess --> Tool
    ToolFileAccess --> FilesShared
    ToolFileAccessShared --> Tool
    ToolCommand --> ToolCommandShared
    ToolCommand --> Tool
    ToolCommand --> Process
    ToolCommand --> Shared
    ToolCommandJson --> ToolJson
    ToolCommandJson --> ToolCommand
    ToolCommandJson --> ToolCommandShared
    ToolCommandJson --> Tool
    ToolCommandJson --> Shared
    ToolCommandShared --> Tool
    ToolCommandShared --> Shared
    ToolContract --> Tool
    ToolContract --> ExecutionShared
    ToolContract --> TasksShared
    ToolDotnet --> ToolCommand
    ToolDotnet --> ToolCommandShared
    ToolDotnet --> Tool
    ToolDotnet --> Shared
    ToolFile --> Tool
    ToolFile --> ToolFileAccessShared
    ToolFile --> ToolFileAccess
    ToolFile --> Shared
    ToolFile --> FilesShared
    ToolGit --> ToolCommand
    ToolGit --> ToolCommandShared
    ToolGit --> Tool
    ToolGit --> Shared
    ToolKuroeInfo --> Shared
    ToolKuroeInfo --> Tool
    ToolKuroeInfo --> ToolJson
    ToolKuroeInfo --> TasksShared
    ToolKuroeInfo --> ExecutionShared
    ToolKuroeInfo --> ControlShared
    ToolKuroeInfo --> PresentationShared
    ToolKuroeInfo --> TasksPresentationShared
    ToolSource --> Tool
    ToolSource --> ToolFileAccessShared
    ToolSource --> ToolFileAccess
    ToolSource --> FilesShared
    ToolTime --> Tool
    ToolJson --> Tool
    ToolJson --> Shared

    %% assistant 与 host 域：装配根向下依赖各域实现
    Assistant --> Core
    Assistant --> Shared
    Assistant --> NodeLlm
    Assistant --> RunnerShared
    Assistant --> RunnerLlm
    Assistant --> Tool
    Assistant --> ToolJson
    Assistant --> ToolKuroeInfo
    Assistant --> ToolSource
    Assistant --> CodeFlowPersistence
    Assistant --> CodeFlowRecordsShared
    Assistant --> FilesShared
    Assistant --> ControlRuntime
    Assistant --> ControlShared
    Assistant --> ExecutionRecordsShared
    Assistant --> ExecutionShared
    Assistant --> InputShared
    Assistant --> PresentationShared
    Assistant --> TasksPersistenceFilesShared
    Assistant --> TasksPersistenceRestoringShared
    Assistant --> TasksPersistenceSavingShared
    Assistant --> TasksPersistenceSerializationShared
    Assistant --> TasksPresentationShared
    Assistant --> TasksRecordsShared
    Assistant --> TasksRuntime
    Assistant --> TasksShared
    Cli --> Core
    Cli --> Shared
    Cli --> ExecutionShared
    Cli --> FlowHosting
    Cli --> TasksRuntime
    Cli --> NodeLlm
    Cli --> TasksShared
    Cli --> ControlShared
    Cli --> PresentationShared
    Cli --> TasksPresentationShared
    Console --> Core
    Console --> Assistant
    Console --> NodeLlm
    Console --> RunnerLlm
    Console --> NodeInput
    Console --> NodeApproval
    Console --> InputShared
    Console --> Tool
    Console --> ToolJson
    Console --> FlowHosting
    Console --> TasksRuntime
    Console --> TasksPersistenceRestoringShared
    Console --> ExecutionShared
    Console --> FlowCompilationShared
    Console --> Shared
    Console --> TasksShared
    Console --> ControlShared
    Console --> PresentationShared
    Console --> TasksPresentationShared
    Core --> Tool
    Core --> ToolTime
    Core --> ToolFile
    Core --> ToolGit
    Core --> ToolDotnet
    Core --> ToolJson
    Core --> ToolCommandJson
    Core --> ToolContract
    Core --> ToolKuroeInfo
    Core --> Shared
    Core --> FilesShared
    Core --> Files
    Core --> ExecutionShared
    Core --> TasksRecordsShared
    Core --> TasksPersistenceSerializationShared
    Core --> TasksPersistenceFilesShared
    Core --> TasksPersistenceRestoring
    Core --> TasksPersistenceRestoringShared
    Core --> TasksPersistenceSaving
    Core --> TasksPersistenceSavingShared
    Core --> TasksPersistenceFiles
    Core --> TasksPersistenceJson
    Core --> TasksRuntime
    Core --> ControlRuntime
    Core --> InputShared
    Core --> InputRuntime
    Core --> Execution
    Core --> NodeLlm
    Core --> RunnerShared
    Core --> RunnerLlm
    Core --> TasksShared
    Core --> ControlShared
    Core --> PresentationShared
    Core --> TasksPresentationShared

    %% platform 域：路径、文件与进程是依赖叶子
    Files --> FilesShared
```

## 项目文档索引

文档分层与维护规则见 [docs-rules](../../docs-rules.md)。

下表按依赖图的域分组，给出各模块的边界文档。域目录与分组同名。

| 模块 | 域 | 边界文档 |
| --- | --- | --- |
| `Kuroe.Execution.Shared` | kernel | [kuroe-execution-shared](functional_boundary/kernel/kuroe-execution-shared.md) |
| `Kuroe.Execution` | kernel | [kuroe-execution](functional_boundary/kernel/kuroe-execution.md) |
| `Kuroe.Execution.Records.Shared` | kernel | [kuroe-execution-records-shared](functional_boundary/kernel/kuroe-execution-records-shared.md) |
| `Kuroe.Input.Shared` | kernel | [kuroe-input-shared](functional_boundary/kernel/kuroe-input-shared.md) |
| `Kuroe.Input.Runtime` | kernel | [kuroe-input-runtime](functional_boundary/kernel/kuroe-input-runtime.md) |
| `Kuroe.Control.Shared` | kernel | [kuroe-control-shared](functional_boundary/kernel/kuroe-control-shared.md) |
| `Kuroe.Control.Runtime` | kernel | [kuroe-control-runtime](functional_boundary/kernel/kuroe-control-runtime.md) |
| `Kuroe.Tasks.Shared` | kernel | [kuroe-tasks-shared](functional_boundary/kernel/kuroe-tasks-shared.md) |
| `Kuroe.Tasks.Runtime` | kernel | [kuroe-tasks-runtime](functional_boundary/kernel/kuroe-tasks-runtime.md) |
| `Kuroe.Tasks.Presentation.Shared` | kernel | [kuroe-tasks-presentation-shared](functional_boundary/kernel/kuroe-tasks-presentation-shared.md) |
| `Kuroe.Tasks.Records.Shared` | kernel | [kuroe-tasks-records-shared](functional_boundary/kernel/kuroe-tasks-records-shared.md) |
| `Kuroe.Presentation.Shared` | kernel | [kuroe-presentation-shared](functional_boundary/kernel/kuroe-presentation-shared.md) |
| `Kuroe.Tasks.Persistence.Files.Shared` | kernel | [kuroe-tasks-persistence-files-shared](functional_boundary/kernel/kuroe-tasks-persistence-files-shared.md) |
| `Kuroe.Tasks.Persistence.Files` | kernel | [kuroe-tasks-persistence-files](functional_boundary/kernel/kuroe-tasks-persistence-files.md) |
| `Kuroe.Tasks.Persistence.Saving.Shared` | kernel | [kuroe-tasks-persistence-saving-shared](functional_boundary/kernel/kuroe-tasks-persistence-saving-shared.md) |
| `Kuroe.Tasks.Persistence.Saving` | kernel | [kuroe-tasks-persistence-saving](functional_boundary/kernel/kuroe-tasks-persistence-saving.md) |
| `Kuroe.Tasks.Persistence.Restoring.Shared` | kernel | [kuroe-tasks-persistence-restoring-shared](functional_boundary/kernel/kuroe-tasks-persistence-restoring-shared.md) |
| `Kuroe.Tasks.Persistence.Restoring` | kernel | [kuroe-tasks-persistence-restoring](functional_boundary/kernel/kuroe-tasks-persistence-restoring.md) |
| `Kuroe.Tasks.Persistence.Serialization.Shared` | kernel | [kuroe-tasks-persistence-serialization-shared](functional_boundary/kernel/kuroe-tasks-persistence-serialization-shared.md) |
| `Kuroe.Tasks.Persistence.Json` | kernel | [kuroe-tasks-persistence-json](functional_boundary/kernel/kuroe-tasks-persistence-json.md) |
| `Kuroe.Flow.Graph.Shared` | flow | [kuroe-flow-graph-shared](functional_boundary/flow/kuroe-flow-graph-shared.md) |
| `Kuroe.Flow.Records.Shared` | flow | [kuroe-flow-records-shared](functional_boundary/flow/kuroe-flow-records-shared.md) |
| `Kuroe.Flow.Compilation.Shared` | flow | [kuroe-flow-compilation-shared](functional_boundary/flow/kuroe-flow-compilation-shared.md) |
| `Kuroe.Flow.Compilation` | flow | [kuroe-flow-compilation](functional_boundary/flow/kuroe-flow-compilation.md) |
| `Kuroe.Flow.NodeLibrary` | flow | [kuroe-flow-nodelibrary](functional_boundary/flow/kuroe-flow-nodelibrary.md) |
| `Kuroe.Flow.NodeType.Container` | flow | [kuroe-flow-nodetype-container](functional_boundary/flow/kuroe-flow-nodetype-container.md) |
| `Kuroe.Flow.Control` | flow | [kuroe-flow-control](functional_boundary/flow/kuroe-flow-control.md) |
| `Kuroe.Flow.Hosting` | flow | [kuroe-flow-hosting](functional_boundary/flow/kuroe-flow-hosting.md) |
| `Kuroe.Flow.Persistence.Graph` | flow | [kuroe-flow-persistence-graph](functional_boundary/flow/kuroe-flow-persistence-graph.md) |
| `Kuroe.Document.Shared` | document | [kuroe-document-shared](functional_boundary/document/kuroe-document-shared.md) |
| `Kuroe.Document.Json.Shared` | document | [kuroe-document-json-shared](functional_boundary/document/kuroe-document-json-shared.md) |
| `Kuroe.Document.Json` | document | [kuroe-document-json](functional_boundary/document/kuroe-document-json.md) |
| `Kuroe.CodeFlow.Records.Shared` | codeflow | [kuroe-codeflow-records-shared](functional_boundary/codeflow/kuroe-codeflow-records-shared.md) |
| `Kuroe.CodeFlow.Persistence` | codeflow | [kuroe-codeflow-persistence](functional_boundary/codeflow/kuroe-codeflow-persistence.md) |
| `Kuroe.Maf.Records.Shared` | maf | [kuroe-maf-records-shared](functional_boundary/maf/kuroe-maf-records-shared.md) |
| `Kuroe.Maf.Definition` | maf | [kuroe-maf-definition](functional_boundary/maf/kuroe-maf-definition.md) |
| `Kuroe.Maf.Compilation.Shared` | maf | [kuroe-maf-compilation-shared](functional_boundary/maf/kuroe-maf-compilation-shared.md) |
| `Kuroe.Maf.Compilation` | maf | [kuroe-maf-compilation](functional_boundary/maf/kuroe-maf-compilation.md) |
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
| `Kuroe.Tool.Command.Shared` | tool | [kuroe-tool-command-shared](functional_boundary/tool/kuroe-tool-command-shared.md) |
| `Kuroe.Tool.Command` | tool | [kuroe-tool-command](functional_boundary/tool/kuroe-tool-command.md) |
| `Kuroe.Tool.Command.Json` | tool | [kuroe-tool-command-json](functional_boundary/tool/kuroe-tool-command-json.md) |
| `Kuroe.Tool.Contract` | tool | [kuroe-tool-contract](functional_boundary/tool/kuroe-tool-contract.md) |
| `Kuroe.Tool.Dotnet` | tool | [kuroe-tool-dotnet](functional_boundary/tool/kuroe-tool-dotnet.md) |
| `Kuroe.Tool.Git` | tool | [kuroe-tool-git](functional_boundary/tool/kuroe-tool-git.md) |
| `Kuroe.Tool.File` | tool | [kuroe-tool-file](functional_boundary/tool/kuroe-tool-file.md) |
| `Kuroe.Tool.KuroeInfo` | tool | [kuroe-tool-kuroeinfo](functional_boundary/tool/kuroe-tool-kuroeinfo.md) |
| `Kuroe.Tool.Source` | tool | [kuroe-tool-source](functional_boundary/tool/kuroe-tool-source.md) |
| `Kuroe.Tool.Time` | tool | [kuroe-tool-time](functional_boundary/tool/kuroe-tool-time.md) |
| `Kuroe.Assistant` | assistant | [kuroe-assistant](functional_boundary/assistant/kuroe-assistant.md) |
| `Kuroe` | host | [kuroe](functional_boundary/host/kuroe.md) |
| `Kuroe.Console` | host | [kuroe-console](functional_boundary/host/kuroe-console.md) |
| `Kuroe.Cli` | host | [kuroe-cli](functional_boundary/host/kuroe-cli.md) |
| `Kuroe.Shared` | platform | [kuroe-shared](functional_boundary/platform/kuroe-shared.md) |
| `Kuroe.Files.Shared` | platform | [kuroe-files-shared](functional_boundary/platform/kuroe-files-shared.md) |
| `Kuroe.Files` | platform | [kuroe-files](functional_boundary/platform/kuroe-files.md) |
| `Kuroe.Process` | platform | [kuroe-process](functional_boundary/platform/kuroe-process.md) |

## 命名空间与路径

命名空间根与项目名一致，程序集内按目录继续分段。`Kuroe.Runner.Shared` 是唯一例外，命名空间根为 `Kuroe.Runner`。`Kuroe.Flow.Graph.Shared` 的分段固定为 `Model`、`StaticGraph`、`RuntimeGraph`、`Runtime.Nodes` 与 `Graph.Records`，`Kuroe.Flow.Control` 的命名空间全部在 `Kuroe.Flow.Control.Runtime` 之下。

`Source/Kuroe.Kernel` 下的 `Control`、`Execution`、`Input`、`Tasks`、`Presentation` 只是路径分组，`Source/Kuroe` 下的 `Node`、`Runner`、`Tool`、`Assistant` 同样只是路径分组。`Kuroe.Document`、`Kuroe.Process`、`Kuroe.Files`、`Kuroe.Console`、`Kuroe.Cli` 位于 `Source` 顶层。

命名空间归属与引用约束由架构测试固定，见[架构测试](../架构测试.md)。
