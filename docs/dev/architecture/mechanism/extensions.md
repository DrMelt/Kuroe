# 新增流程实现与扩展点

新增流程实现只需实现控制契约并注册自己的记录与编解码，不修改 Kernel 与中央序列化。本文给出接入顺序与全部扩展点。

## 新增流程实现

1. 实现 `IFlowIdentity`，按流程名与 `FlowIdentity` 认领提交与恢复。
2. 实现 `IFlowSubmission`、`IFlowState`、`IFlowControlFactory` 与 `IFlowControl`；需要恢复时另实现 `IFlowStateBinder`，支持按步骤重跑时另实现 `IFlowRerun`。
3. 需要外部输入时，由步骤的执行函数登记等待，中断与重新派发由控制实例负责，见[流程控制与任务域](flow-control.md)。
4. 实现 `IFlowPresentation` 的 `CaptureView` 与 `IFlowRecordSource` 的 `CaptureRecord`。等待步骤由任务视图提供，表现不另设入口。
5. 把端口组合为 `FlowProvider` 注册。状态绑定与流程记录来源按需提供，不需要的能力以空值表达，不使用空实现对象。
6. 提供自有定义记录、状态记录与流程种类常量，实现 `IFlowRecordCodec` 并注册。编解码错误经返回值报告。
7. 需要进入流程模板目录时单独注册 `IFlowTemplateSource`，由中性模板目录聚合。
8. 在 `ExecutionRequest.OutputPorts` 声明输出端口后直接使用中性契约工具。
9. 由 MAF 运行期承载推进时，把步骤执行桥接到 `IFlowExecutionGateway`，每个步骤建立一次执行。

新增流程实现不构造 `TaskSnapshot`，不捕获执行记录。任务快照装配与执行记录捕获由任务域完成。

## 扩展点

| 能力 | 接口 |
| --- | --- |
| 流程实现 | `IFlowControl`、`IFlowControlFactory`、`IFlowIdentity`、`IFlowProviderSource`、`FlowProvider`、`FlowIdentity` |
| 流程推进 | `IFlowRuntime.Rerun`、等待执行重新派发 |
| 输入通道 | `IInputChannel`、`IInputDelivery`、`InputReceiver` |
| 执行运行器 | `IExecutionRunner`、`IExecutionRunnerFactory`、`IExecutionBinder`、`IExecutionRestorer`、`ExecutionInvocation`、`IExecutionRequestEntry` |
| 图节点能力 | `IGraphNodeCapabilities`、`INodeOutputProjection`、`INodeRuntimeFactory` |
| MAF 步骤能力 | `IMafNodeExecutions`、`IMafStepInvocationFactory` |
| MAF 步骤执行装配 | `IMafStepInvocationFactory`、`MafStepInvocationFactory` |
| 任务表现 | `IFlowPresentation`、`IFlowView`、`ITaskFlowView`、`TaskSnapshotFactory` |
| 流程记录 | `IFlowRecordSource`、`IFlowDefinitionRecord`、`IFlowStateRecord` |
| 存档片段编解码 | `IFlowRecordCodec`、`FlowRecordData` |
| 流程提交与恢复 | `IFlowSubmission`、`IFlowStateBinder` |
| 流程重跑 | `IFlowRerun` |
| 流程模板 | `IFlowTemplateSource`、`IFlowTemplateCatalog` |
| 节点类型 | `INodeTypeDescription`、`INodeTypeValidation`、`INodeType` |
| 工具 | `ITool`、`IToolSource`、`IToolFile`、`IToolContractCatalog` |

## 节点与工具的接入

节点类型只提供类型名、静态形状与实例建立，参数段按字段声明读取，节点规则由模型层给出并在流程编译时校验。执行器绑定与图节点能力由节点项目按类型提供，图流程与 MAF 各自注册所需能力。

工具以 `ITool` 给出函数声明，以 `IToolSource` 在装配期提供定义并在运行期按执行上下文创建载体。文件配置的工具经 `IToolFile` 注册到工具文件注册表，注册必须显式，同名处理见[同名覆盖与重名](overrides.md)。