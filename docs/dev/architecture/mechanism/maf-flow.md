# MAF 流程

MAF 流程把步骤推进交给 Microsoft Agent Framework 运行期，步骤的每次运行仍是一次 Kuroe 执行。流程定义由宿主以代码给出，或由流程描述装配，不写入 `.kuroe` 目录。流程定义类型位于 `Kuroe.Maf.Definition`，运行实例创建与任务控制位于 `Kuroe.Maf.Control`，文档装配位于 `Kuroe.Maf.Compilation`，装配入口 `AddKuroeMafFlows` 位于 `Kuroe.Host.FlowIntegration.Maf`。

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
| `StartPorts` | 开始端口与默认值，必须包含 `MODEL_NAME` |
| `Steps` | 步骤标识、标签、输出端口与执行装配工厂 |
| `Edges` | 步骤之间的连接 |
| `Description` | 流程说明，进入流程目录 |

步骤标识同时是执行步骤标识与流程步骤路径，标识不能为空，也不能含 `/`。执行装配结果的 `ExecutionInvocation.Step` 必须与步骤标识一致，提交时校验。一个步骤至多一个上游，分叉后的步骤可以并行执行，多个上游的收敛不支持。

## 输入与产出

提交时按开始端口校验输入，省略的端口用默认值补齐。`MODEL_NAME` 必填，模型名交给每个步骤建立执行装配。

入口步骤的输入是开始端口文本，`MODEL_NAME` 不进入执行输入。其余步骤的输入是直接上游步骤的输出端口。步骤的 `Outputs` 声明需要交回的执行端口，交回的端口文本作为消息交给下游，更早步骤的产出不再向后传递。

## 执行与恢复

每个步骤建立一次执行，执行记录、用量、中断与恢复数据由任务域持有。任务存档保存开始状态、运行期检查点与未走完的步骤，任务存档是任务级权威记录。

步骤执行被中断时，当前 MAF 运行停止；中断上报只收口发起它的运行，恢复后的运行不受旧运行收口影响。任务仍在推进时暂停等待恢复；任务已由暂停或终止收口时保持原控制状态。恢复放弃当前检查点，从入口重新执行全部步骤；步骤之间暂停时从最近检查点继续。步骤失败时任务停在阻塞，重新执行放弃检查点并从入口重新执行全部步骤。

MAF 流程不提供停驻面，不能等待外部输入或批准，其任务没有等待步骤。

## 文档装配与来源语义

MAF 文档装配消费流程描述编译与流程语义。流程描述编译保证语言合法，流程语义给出来源终点与接线结果，MAF 只做目标适配：按来源终点决定步骤输入取常量、开始输入还是上游交回，按目标模型判定不能表达的结构。

装配入口位于 `Kuroe.Host.FlowIntegration.Maf`，文档装配实现在 `Kuroe.Maf.Compilation`，语言规则与来源解析不在 MAF 侧。

## 可装配子集

MAF 目标模型只承载文本步骤与单上游连接。以下语言能力可装配：

- 单入口流程。
- 每步骤至多一个上游步骤。
- 文本输入与文本输出。
- 开始端口注入、输入默认值与装配期常量。
- 运行期常量与上游端口交回。
- 容器作为命名空间，成员提升为步骤，容器输出映射到成员端口。

以下语言能力不可装配，由 MAF 装配给出目标不可表达诊断：

- 等待外部输入的节点类型。
- 多上游汇合与容器多成员汇合。
- 多入口流程。
- 触发端口与触发条件。
- 上下文端口与信号端口。
- 需要环外入口的环推进。环推进在入口处形成多上游，由多上游约束拒绝。

执行上限与多次发布不单独判定：MAF 每步骤执行一次，线性流程中逐次发布与首次发布行为一致。

## 区域约束

MAF 流程实现位于 `Source/Kuroe/Maf`，装配入口位于 `Source/Kuroe/Host/FlowIntegration/Maf`，不属于 `Kuroe.Flow` 区域，对 `Kuroe.Flow` 没有项目引用。文档装配位于 `Kuroe.Maf.Compilation`，引用流程描述的定义、编译与语义契约，不引用图流程实现。语言规则与来源解析由流程描述承担，MAF 只做目标适配与可表达性判定。Workflows 包只出现在 `Kuroe.Maf.Control`，`Kuroe.Host.Composition` 不引用 MAF 运行期。