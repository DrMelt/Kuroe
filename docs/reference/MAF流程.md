# MAF 流程

MAF 流程把步骤推进交给 Microsoft Agent Framework 运行期，步骤的每次运行仍是一次 Kuroe 执行。流程定义由宿主以代码给出，不写入 `.kuroe` 目录。流程定义类型位于 `Kuroe.Maf.Definition`，运行实例创建与任务控制位于 `Kuroe.Maf.Control`。装配入口 `AddKuroeMafFlows` 位于 `Kuroe.Maf`。

## 宿主装配

宿主在核心装配之后注册流程定义：

```csharp
services.AddKuroeMafFlows(
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
                new MafStepTarget(typeof(LlmExecutionRunnerFactory), model => CreateParameters(model))),
            new MafStep(
                new MafStepId("执行"),
                "执行",
                [new PortName("结论")],
                new MafStepTarget(typeof(LlmExecutionRunnerFactory), model => CreateParameters(model))),
        ],
        [
            new MafEdge(new MafStepId("计划"), new MafStepId("执行")),
        ])
    {
        Description = "先计划再执行",
    });
```

`CreateParameters` 返回 `ErrorOr<IExecutionParameters>`，由宿主按模型构造参数。`MafStepTarget` 由框架建立执行目标，目标标识直接来自步骤标识，运行器按目标执行运行器键选择。宿主必须注册具有该键的执行器工厂。需要自定义执行目标时实现 `IMafStepTargetFactory`，提交时校验目标标识必须与步骤标识一致。`AddKuroeMafFlows` 同时注册流程实现、存档编解码与流程模板，流程进入流程目录，可以从 Console 与 CLI 按名字提交。传入 `IMafFlowSource` 时按该来源取定义。

## 定义

| 成员 | 说明 |
| --- | --- |
| `Name` | 流程名，提交与恢复按它选择实现 |
| `StartPorts` | 开始端口与默认值，必须包含 `MODEL_NAME` |
| `Steps` | 步骤标识、标签、输出端口与执行目标工厂 |
| `Edges` | 步骤之间的连接 |
| `Description` | 流程说明，进入流程目录 |

步骤标识同时是执行目标标识与流程步骤路径，标识不能为空，也不能含 `/`。执行目标工厂给出的 `IExecutionTarget.Id` 必须与步骤标识一致，提交时校验。一个步骤至多一个上游，分叉后的步骤可以并行执行，多个上游的收敛不支持。

## 输入与产出

提交时按开始端口校验输入，省略的端口用默认值补齐。`MODEL_NAME` 必填，模型名交给每个步骤建立执行目标。

入口步骤的输入是开始端口文本，`MODEL_NAME` 不进入执行输入。其余步骤的输入是直接上游步骤的输出端口。步骤的 `Outputs` 声明需要交回的执行端口，交回的端口文本作为消息交给下游，更早步骤的产出不再向后传递。

## 执行与恢复

每个步骤建立一次执行，执行记录、用量、中断与恢复数据由任务域持有。任务存档保存开始状态、运行期检查点与未走完的步骤，任务存档是任务级权威记录。

步骤执行被中断时，当前 MAF 运行停止。任务仍在推进时暂停等待恢复；任务已由暂停或终止收口时保持原控制状态。恢复放弃当前检查点，从入口重新执行全部步骤；步骤之间暂停时从最近检查点继续。步骤失败时任务停在阻塞，重新执行放弃检查点并从入口重新执行全部步骤。

MAF 流程不提供停驻面，不能等待外部输入或批准。