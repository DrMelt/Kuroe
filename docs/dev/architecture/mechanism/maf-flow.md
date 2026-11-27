# MAF 流程

MAF 流程把步骤推进交给 Microsoft Agent Framework 运行期，步骤的每次运行仍是一次 Kuroe 执行。流程定义由宿主以代码给出，或由 `.kuroe/flows.json` 的流程描述装配，MAF 流程定义不写回工作目录。图流程与 MAF 各自装配同一份流程描述，装配成功的实现各提供一个流程变体，提交时按实现选择。流程定义类型位于 `Kuroe.Maf.Definition`，运行实例创建与任务控制位于 `Kuroe.Maf.Control`，文档装配位于 `Kuroe.Maf.Compilation`，装配入口 `AddKuroeMafFlows` 位于 `Kuroe.Host.FlowIntegration.Maf`。

## 宿主装配

宿主在 `Kuroe.Host.Composition` 装配之后注册流程定义。`composition` 是 `AddKuroeComposition` 的返回值，`FlowTemplates` 用于登记流程模板，`LlmRunner` 是由 `Kuroe.Host.Composition` 预建的 Llm 执行运行器工厂：

```csharp
services.AddKuroeMafFlows(
    composition.FlowTemplates,
    new MafFlowDefinition(
        new FlowName("计划执行"),
        [
            new MafStartPort(new PortName("目标")),
            new MafStartPort(MafFlowDefinition.ModelPort),
        ],
        [
            new MafStep(
                new MafStepId("计划"),
                "计划",
                [new PortName("计划")],
                new MafStepInvocationFactory(composition.LlmRunner, context => CreateParameters(context))),
            new MafStep(
                new MafStepId("执行"),
                "执行",
                [new PortName("结论")],
                new MafStepInvocationFactory(composition.LlmRunner, context => CreateParameters(context))),
        ],
        [
            new MafEdge(new MafStepId("计划"), new MafStepId("执行")),
        ])
    {
        Description = "先计划再执行",
    });
```

`CreateParameters` 接收 `ExecutionParameterContext`，返回 `ErrorOr<IExecutionParameters>`，由宿主按模型与当前轮次构造参数。步骤的每次运行建立一次参数。`MafStepInvocationFactory` 持有执行运行器工厂实例，由框架建立执行装配结果，装配结果使用步骤标识。需要自定义执行装配时实现 `IMafStepInvocationFactory`，提交时校验装配结果的步骤标识必须与步骤标识一致。`AddKuroeMafFlows` 同时注册流程实现、存档编解码与流程模板，流程进入流程目录，可以从 Console 与 CLI 按名字提交。传入 `IMafFlowSource` 时按该来源取定义。

## 定义

| 成员 | 说明 |
| --- | --- |
| `Name` | 流程名，提交与恢复按它选择实现 |
| `StartPorts` | 开始端口、默认值、取值种类与模式，必须包含 `MODEL_NAME` |
| `Steps` | 步骤标识、标签、输出端口、输出计划与执行装配工厂 |
| `Edges` | 步骤之间的连接 |
| `Description` | 流程说明，进入流程目录 |

步骤标识同时是执行步骤标识与流程步骤路径，标识不能为空，也不能含 `/`。执行装配结果的 `ExecutionInvocation.Step` 必须与步骤标识一致，提交时校验。

定义校验由 `Validate` 承担：步骤标识合法且不重复、输出端口合法、连接端点存在、至少一个入口、全部步骤从任一入口可达、开始端口合法且包含 `MODEL_NAME`。多入口与多上游属于结构本身，`Entries` 给出全部入口。

## 输入与产出

提交时按开始端口校验输入，省略的端口用默认值补齐。`MODEL_NAME` 必填，模型名交给每个步骤建立执行装配。

入口步骤的输入由开始端口计划给出，`MODEL_NAME` 不进入执行输入。其余步骤的输入由来源终点计划给出。每个输入绑定携带端口种类、执行角色、启动组、触发条件与来源；同一端口可以有多条绑定。输出计划携带端口种类、触发标记与交回要求。`Context` 由来源透传，`Signal` 只表达触发位。

控制层按步骤节点消费这些计划。开始执行器向全部入口发送初始消息，步骤节点按输入绑定累积来源，按组判定齐备，齐备后执行一次并把产出交给下游。步骤在一次运行内只执行一次，执行状态随工作流状态保存，从检查点恢复不会重复执行。端口种类、触发条件、组内 AND、组间 OR 与同端口多来源在运行路径生效。开始端口种类与输出计划进入视图与模板。

## 执行与恢复

每个步骤建立一次执行，执行记录、用量、中断与恢复数据由任务域持有。任务存档保存开始状态、运行期检查点与未走完的步骤，任务存档是任务级权威记录。

步骤执行被中断时，当前 MAF 运行停止；中断上报只收口发起它的运行，恢复后的运行不受旧运行收口影响。任务仍在推进时暂停等待恢复；任务已由暂停或终止收口时保持原控制状态。恢复放弃当前检查点，从入口重新执行全部步骤；步骤之间暂停时从最近检查点继续。步骤失败时任务停在阻塞，重新执行放弃检查点，等旧运行退出后从入口启动新运行，避免旧运行事件重新标记失败。

MAF 流程不提供停驻面，不能等待外部输入或批准，其任务没有等待步骤。

## 文档装配与来源语义

MAF 文档装配消费流程描述编译与流程语义。流程描述编译保证语言合法，流程语义给出来源终点与接线结果，MAF 只做目标适配：按来源终点决定步骤输入取常量、开始输入还是上游交回，并保留目标结构诊断。

文档装配逐流程进行。某个流程编译失败时只淘汰该流程的 MAF 变体，其他流程继续装配并进入 MAF 来源。图流程同时装配同一份文档，装配成功的图流程变体不受影响。两个实现都失败的流程由宿主在启动时给出两份原因，单个实现失败只影响对应变体。

装配入口位于 `Kuroe.Host.FlowIntegration.Maf`，文档装配实现在 `Kuroe.Maf.Compilation`，语言规则与来源解析不在 MAF 侧。图流程与 MAF 的共同装配由 `Kuroe.Host.FlowIntegration.Flows` 的入口完成：先建立图流程装配结果，再把同一份文档交给 MAF 逐流程装配，两个实现各自登记流程模板。

## 第一阶段验证子集

目标模型与文档装配当前承载以下描述构造：

- 端口种类：`Context`、`Signal`、按语义映射的自定义类型。`Context` 输出按执行输入内容合并透传，来源描述随内容保留，执行器交回的 `Context` 端口内容不采用。
- 端口模式与条件：触发模式、常量模式、控制角色、正向条件、反向条件。
- 开始端口：承载内容的开始端口需要提交或由宿主给值，不承载内容的开始端口自动放行，固定值开始端口作为装配期常量。
- 模型：节点类型声明的模型端口决定流程是否需要 `MODEL_NAME`，不需要模型的流程不要求提交模型；需要模型时由宿主当前模型补入开始输入。
- 连接与取值：同端口多来源、组内 AND、组间 OR、运行期常量、静态依赖链、多入口。
- 执行声明：`Emission.Once` 决定运行期常量端口是否要求交回。
- 容器：成员提升为独立步骤，容器输出解析到成员端口。

开始触发端口由流程描述语言拒绝，不属于 MAF 目标支持范围。固定模型由 MAF 目标拒绝，模型由提交给出。

以下构造仍后置：

- 等待外部输入的节点类型。
- 从输入派生的输出。
- 不派发执行的类型。
- 容器成员齐备与容器输入接线。容器仍按现有成员提升方式处理。
- 环推进与多次执行。步骤在一次运行内只执行一次，`MaxExecutions` 与重复发布随环推进接入。

上述构造已接入文档装配与控制运行路径，Console 与 CLI 已接入。图流程与 MAF 同时装配流程描述，两个实现都支持的流程在流程目录中生成两个变体。容器整体推进、等待、结构节点、环与多次执行仍不进入第一阶段。

## 区域约束

MAF 流程实现位于 `Source/Kuroe/Maf`，装配入口位于 `Source/Kuroe/Host/FlowIntegration/Maf`，不属于 `Kuroe.Flow` 区域，对 `Kuroe.Flow` 没有项目引用。图流程与 MAF 的共同装配入口位于 `Kuroe.Host.FlowIntegration.Flows`，它引用两个实现并为每条流程登记变体。文档装配位于 `Kuroe.Maf.Compilation`，引用流程描述的定义、编译与语义契约，不引用图流程实现。语言规则与来源解析由流程描述承担，MAF 只做目标适配与可表达性判定。Workflows 包只出现在 `Kuroe.Maf.Control`，`Kuroe.Host.Composition` 不引用 MAF 运行期。