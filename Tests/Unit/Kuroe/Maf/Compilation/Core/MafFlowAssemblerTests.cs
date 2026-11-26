using ErrorOr;
using Kuroe.Host.Ability.Node.Llm;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Compilation;
using Kuroe.FlowDescription.Compilation.Library;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Semantics;
using Kuroe.Maf.Definition;
using Kuroe.TestSupport;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Maf.Compilation.Tests;

/// <summary>流程描述到 MAF 流程定义的装配：单链、输入装配与不能表达的结构。</summary>
public sealed class MafFlowAssemblerTests
{
    private static readonly PortName Goal = new("目标");
    private static readonly PortName Output = new("输出");
    private static readonly PortName Input = new("输入");
    private static readonly PortName Result = new("结果");
    private static readonly PortName Outline = new("大纲");
    private static readonly PortName WorkPath = new("TASK_WORK_PATH");

    [Fact(DisplayName = "装配单链步骤与连接")]
    public void Assembles_a_single_chain()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Chain());

        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));
        MafFlowDefinition flow = Assert.Single(assembled.Value);
        Assert.Equal(2, flow.Steps.Count);
        MafEdge edge = Assert.Single(flow.Edges);
        Assert.Equal(flow.Steps[0].Id, edge.From);
        Assert.Equal(flow.Steps[1].Id, edge.To);
        Assert.Contains(flow.StartPorts, port => port.Name == Goal);
        Assert.Contains(flow.StartPorts, port => port.Name == MafFlowDefinition.ModelPort);
        Assert.Equal([Output.Value], flow.Steps[0].Outputs.Select(port => port.Value));
    }

    [Fact(DisplayName = "输入装配取开始输入与上游端口")]
    public void Inputs_take_start_inputs_and_upstream_outputs()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Chain());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));
        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep first = flow.Steps[0];
        MafStep second = flow.Steps[1];

        Assert.Equal("做点事", Single(first, Goal, []).Text);
        Assert.Equal("test-model", Single(first, MafFlowDefinition.ModelPort, []).Text);

        MafStepInput input = Single(second, Input, [new MafStepOutput(first.Id, Output, "计划")]);
        Assert.Equal("计划", input.Text);
        Assert.Equal(first.Id, input.UpstreamStep);
    }

    [Fact(DisplayName = "文档来源按名提供装配结果")]
    public void Document_source_finds_assembled_flows()
    {
        MafFlowAssembler assembler = new(new FlowDescriptionCompiler(TestTypeFiles.Registry(), new NodeLibraryExpander(), new FlowSemanticsAnalyzer()), Executions());
        ErrorOr<MafDocumentFlowSource> created = MafDocumentFlowSource.Create(assembler, Chain());

        Assert.False(created.IsError, Describe(created.ErrorsOrEmptyList));
        Assert.Single(created.Value.All());
        Assert.True(created.Value.Find(new FlowName("单链")).IsSuccess);
        Assert.True(created.Value.Find(new FlowName("缺少")).IsError);
    }

    [Fact(DisplayName = "步骤装配给出执行调用")]
    public void Steps_create_execution_invocations()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Chain());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));
        MafStep step = Assert.Single(assembled.Value).Steps[0];

        ErrorOr<ExecutionInvocation> invocation = step.InvocationFactory.Create(
            step.Id,
            "test-model",
            new ExecutionParameterContext(
                new ExecutionOwnerId("1"),
                step.ExecutionStepId,
                1,
                1,
                [],
                [],
                new Dictionary<ExecutionPortName, string>(),
                [],
                "test-model"));

        Assert.False(invocation.IsError, Describe(invocation.ErrorsOrEmptyList));
        Assert.Equal(step.ExecutionStepId, invocation.Value.Step);
    }

    [Fact(DisplayName = "装配多个上游步骤")]
    public void Assembles_multiple_upstream_steps()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Merge());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        Assert.Equal(3, flow.Steps.Count);

        MafStep first = flow.Steps.Single(step => step.Label == "甲");
        MafStep second = flow.Steps.Single(step => step.Label == "乙");
        MafStep merge = flow.Steps.Single(step => step.Label == "汇合点");
        Assert.Equal(2, flow.Edges.Count(edge => edge.To == merge.Id));
        IReadOnlyList<MafStepInput> inputs = merge.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            merge.Id,
            [
                new MafStepOutput(first.Id, new PortName("大纲"), "甲值"),
                new MafStepOutput(second.Id, new PortName("大纲"), "乙值"),
            ],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model"));

        MafStepInput[] merged = [.. inputs.Where(input => input.Port.Value is "甲纲" or "乙纲")];
        Assert.Equal(2, merged.Length);
        Assert.All(merged, input => Assert.Equal(-1, input.Group));
        Assert.All(merged, input => Assert.Equal(MafTriggerCondition.Positive, input.Condition));
        Assert.Equal("甲值", merged.Single(input => input.Port.Value == "甲纲").Text);
        Assert.Equal("乙值", merged.Single(input => input.Port.Value == "乙纲").Text);
        Assert.All(merged, input => Assert.True(input.UpstreamStep is not null));
    }

    private static MafStepInput Single(
        MafStep step,
        PortName port,
        IReadOnlyList<MafStepOutput> upstream) =>
        step.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            step.Id,
            upstream,
            new Dictionary<PortName, string>
            {
                [Goal] = "做点事",
                [MafFlowDefinition.ModelPort] = "test-model",
            },
            "test-model")).Single(input => input.Port == port);

    private static ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(FlowDescriptionSet document)
    {
        FlowDescriptionCompiler compiler = new(TestTypeFiles.Registry(), new NodeLibraryExpander(), new FlowSemanticsAnalyzer());

        return new MafFlowAssembler(compiler, Executions()).Assemble(document);
    }

    private static MafNodeExecutions Executions()
    {
        MafNodeExecutions executions = new();
        executions.Register(LlmNodeTypes.Name, type => LlmNodeExecution.Bind(type, TestTypeFiles.Runners));

        return executions;
    }

    private static string Describe(IReadOnlyList<Error> errors) =>
        string.Join("；", errors.Select(error => error.Description));

    private static FlowDescriptionSet Chain() => new(
        [],
        [
            new FlowEntry("单链", null, [GoalPort(), ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [GoalInput(), ModelInput()],
                Bindings = new Dictionary<string, string>
                {
                    [Goal.Value] = $"@{Goal.Value}",
                    [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                },
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "第一步",
                        Kind = "Llm",
                        Inputs = [GoalInput(), ModelInput()],
                        Bindings = new Dictionary<string, string>
                        {
                            [Goal.Value] = $"@{Goal.Value}",
                            [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                        },
                        Outputs = [new PortDescription(Output.Value, "Text")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "第二步",
                        Kind = "Llm",
                        Inputs = [new InputPortDescription(Input.Value, "Text"), ModelInput()],
                        Bindings = new Dictionary<string, string>
                        {
                            [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                        },
                        From = [new FromGroupDescription([new FromLinkDescription(Input.Value, $"第一步@{Output.Value}")])],
                        Outputs = [new PortDescription(Result.Value, "Text")],
                    },
                ],
            }),
        ]);

    private static PortDescription GoalPort() =>
        new(Goal.Value, "Text", PortModeDescription.Constant);

    private static PortDescription ModelPort() =>
        new(MafFlowDefinition.ModelPort.Value, "Text", PortModeDescription.Constant);

    private static InputPortDescription GoalInput() =>
        new(Goal.Value, "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content);

    private static InputPortDescription ModelInput() =>
        new(MafFlowDefinition.ModelPort.Value, "Text", PortModeDescription.Constant);

    [Fact(DisplayName = "装配多个启动组")]
    public void Assembles_multiple_start_groups()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Groups());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep first = flow.Steps.Single(step => step.Label == "甲");
        MafStep second = flow.Steps.Single(step => step.Label == "乙");
        MafStep selector = flow.Steps.Single(step => step.Label == "选择");
        MafStepInput[] inputs = [.. selector.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            selector.Id,
            [
                new MafStepOutput(first.Id, new PortName("结果"), "甲值"),
                new MafStepOutput(second.Id, new PortName("结果"), "乙值"),
            ],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Where(input => input.Port.Value == "输入")];

        Assert.Equal(2, inputs.Length);
        Assert.Equal([0, 1], [.. inputs.Select(input => input.Group).Order()]);
        Assert.Equal("甲值", inputs.Single(input => input.Group == 0).Text);
        Assert.Equal("乙值", inputs.Single(input => input.Group == 1).Text);
        Assert.Equal(2, flow.Edges.Count(edge => edge.To == selector.Id));
    }

    private static FlowDescriptionSet Groups() => new(
        [],
        [
            new FlowEntry("多组", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "甲",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "乙",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "选择",
                        Kind = "Llm",
                        Inputs = [new InputPortDescription("输入", "Text"), ModelInput()],
                        Bindings = ModelBinding(),
                        From =
                        [
                            new FromGroupDescription([new FromLinkDescription("输入", "甲@结果")]),
                            new FromGroupDescription([new FromLinkDescription("输入", "乙@结果")]),
                        ],
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet Merge() => new(
        [],
        [
            new FlowEntry("汇合", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = new Dictionary<string, string>
                {
                    [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                },
                Nodes =
                [
                    Constant("甲"),
                    Constant("乙"),
                    new NodeDefinitionDescription
                    {
                        Name = "汇合点",
                        Kind = "Llm",
                        Inputs =
                        [
                            new InputPortDescription("甲纲", "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content),
                            new InputPortDescription("乙纲", "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content),
                            ModelInput(),
                        ],
                        Bindings = new Dictionary<string, string>
                        {
                            ["甲纲"] = "甲@大纲",
                            ["乙纲"] = "乙@大纲",
                            [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                        },
                        Outputs = [new PortDescription("选定", "Text")],
                    },
                ],
            }),
        ]);

    [Fact(DisplayName = "静态输入取装配期常量输出")]
    public void Static_input_takes_literal_constant_output()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Literal());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep reader = flow.Steps.Single(step => step.Label == "读取");
        MafStepInput input = reader.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            reader.Id,
            [new MafStepOutput(flow.Steps[0].Id, new PortName("结果"), "动态产出")],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Single(item => item.Port == Input);

        Assert.Equal("固定值", input.Text);
        Assert.Null(input.UpstreamStep);
    }

    [Fact(DisplayName = "装配多个入口步骤")]
    public void Assembles_multiple_entry_steps()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(MultipleEntries());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        Assert.Equal(2, flow.Steps.Count);
        Assert.Empty(flow.Edges);
        Assert.Equal(2, flow.Entries().Count);
        Assert.Contains(flow.Entries(), step => step.Label == "甲");
        Assert.Contains(flow.Entries(), step => step.Label == "乙");
    }

    [Fact(DisplayName = "装配控制角色输入")]
    public void Assembles_control_inputs()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(ControlInput());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep step = Assert.Single(flow.Steps);
        MafStepInput input = step.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            step.Id,
            [],
            new Dictionary<PortName, string>
            {
                [Goal] = "目标值",
                [MafFlowDefinition.ModelPort] = "test-model",
            },
            "test-model")).Single(item => item.Port.Value == "控制");

        Assert.Equal(ExecutionPortRole.Control, input.Role);
        Assert.Equal(ExecutionPortKind.Text, input.Kind);
        Assert.Equal(0, input.Group);
    }

    [Fact(DisplayName = "装配信号端口")]
    public void Assembles_signal_ports()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Signal());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep producer = flow.Steps.Single(step => step.Label == "产生");
        MafStep consumer = flow.Steps.Single(step => step.Label == "放行");
        MafStepInput input = consumer.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            consumer.Id,
            [new MafStepOutput(producer.Id, new PortName("计划"), "计划值")],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Single(item => item.Port.Value == "批准");

        Assert.Equal(ExecutionPortKind.Signal, input.Kind);
        Assert.Equal(ExecutionPortRole.Control, input.Role);
        Assert.Equal("计划值", input.Text);
    }

    [Fact(DisplayName = "固定值开始端口只作为输入取值")]
    public void Injects_fixed_start_ports_as_inputs()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Fixed());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));
        MafFlowDefinition flow = Assert.Single(assembled.Value);

        Assert.DoesNotContain(flow.StartPorts, port => port.Name == WorkPath);
        MafStepInput input = flow.Steps[0].Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            flow.Steps[0].Id,
            [],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Single(item => item.Port == WorkPath);
        Assert.Equal("/tmp/work", input.Text);
    }

    [Fact(DisplayName = "容器输出解析到成员端口")]
    public void Resolves_container_outputs_to_members()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(ContainerOutput());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));
        MafFlowDefinition flow = Assert.Single(assembled.Value);
        Assert.Equal(2, flow.Steps.Count);
        MafEdge edge = Assert.Single(flow.Edges);
        Assert.Equal(flow.Steps[0].Id, edge.From);
        Assert.Contains("撰写", edge.From.Value, StringComparison.Ordinal);

        PortName body = new("正文");
        MafStepInput input = flow.Steps[1].Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            flow.Steps[1].Id,
            [new MafStepOutput(flow.Steps[0].Id, body, "全文")],
            new Dictionary<PortName, string>
            {
                [Outline] = "大纲文本",
                [MafFlowDefinition.ModelPort] = "test-model",
            },
            "test-model")).Single(item => item.Port == new PortName("全文"));
        Assert.Equal("全文", input.Text);
    }

    [Fact(DisplayName = "装配上下文端口与输出计划")]
    public void Assembles_context_ports_and_output_plan()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(ContextFlow());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep producer = flow.Steps.Single(step => step.Label == "提供");
        MafStep consumer = flow.Steps.Single(step => step.Label == "读取");
        MafOutputPort output = Assert.Single(producer.OutputPlan);
        MafStepInput input = consumer.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            consumer.Id,
            [new MafStepOutput(producer.Id, new PortName("上下文"), "上下文值")],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Single(item => item.Port.Value == "上下文");

        Assert.Equal(ExecutionPortKind.Context, output.Kind);
        Assert.False(output.Required);
        Assert.Equal(ExecutionPortKind.Context, input.Kind);
        Assert.Equal(ExecutionPortRole.Content, input.Role);
        Assert.Equal("上下文值", input.Text);
    }

    [Fact(DisplayName = "装配触发端口与反向条件")]
    public void Assembles_trigger_ports_and_negative_condition()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(Trigger());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStep producer = flow.Steps.Single(step => step.Label == "产生");
        MafStep consumer = flow.Steps.Single(step => step.Label == "放行");
        MafOutputPort output = producer.OutputPlan.Single(port => port.Name.Value == "计划");
        MafStepInput input = consumer.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            consumer.Id,
            [new MafStepOutput(producer.Id, new PortName("计划"), "计划值")],
            new Dictionary<PortName, string> { [MafFlowDefinition.ModelPort] = "test-model" },
            "test-model")).Single(item => item.Port.Value == "批准");

        Assert.Equal(ExecutionPortKind.Text, output.Kind);
        Assert.True(output.Trigger);
        Assert.Equal(ExecutionPortKind.Text, input.Kind);
        Assert.Equal(ExecutionPortRole.Content, input.Role);
        Assert.Equal(MafTriggerCondition.Negative, input.Condition);
        Assert.Equal("计划值", input.Text);
    }

    [Fact(DisplayName = "装配无固定值的开始常量端口")]
    public void Assembles_start_constant_port_without_value()
    {
        ErrorOr<IReadOnlyList<MafFlowDefinition>> assembled = Assemble(StartConstant());
        Assert.False(assembled.IsError, Describe(assembled.ErrorsOrEmptyList));

        MafFlowDefinition flow = Assert.Single(assembled.Value);
        MafStartPort constant = flow.StartPorts.Single(port => port.Name == WorkPath);
        MafStep step = Assert.Single(flow.Steps);
        MafStepInput input = step.Inputs.Assemble(new MafStepInputContext(
            new ExecutionOwnerId("1"),
            step.Id,
            [],
            new Dictionary<PortName, string>
            {
                [WorkPath] = "/tmp/work",
                [MafFlowDefinition.ModelPort] = "test-model",
            },
            "test-model")).Single(item => item.Port == WorkPath);

        Assert.Equal(ExecutionPortKind.Text, constant.Kind);
        Assert.Equal(MafPortMode.Constant, constant.Mode);
        Assert.Equal(-1, input.Group);
        Assert.Equal("/tmp/work", input.Text);
    }

    private static FlowDescriptionSet StartConstant() => new(
        [],
        [
            new FlowEntry("开始常量", null,
                [new PortDescription(WorkPath.Value, "Text", PortModeDescription.Constant), ModelPort()],
                new NodeDefinitionDescription
                {
                    Name = "整体",
                    Kind = "Container",
                    Inputs = [WorkPathInput(), ModelInput()],
                    Bindings = WorkPathBinding(),
                    Nodes =
                    [
                        new NodeDefinitionDescription
                        {
                            Name = "写入",
                            Kind = "Llm",
                            Inputs = [WorkPathInput(), ModelInput()],
                            Bindings = WorkPathBinding(),
                            Outputs = [new PortDescription(Result.Value, "Text")],
                        },
                    ],
                }),
        ]);

    private static FlowDescriptionSet Trigger() => new(
        [],
        [
            new FlowEntry("触发", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "产生",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("计划", "Text", PortModeDescription.Trigger)],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "放行",
                        Kind = "Llm",
                        Inputs =
                        [
                            new InputPortDescription("批准", "Text", PortModeDescription.Trigger),
                            ModelInput(),
                        ],
                        Bindings = ModelBinding(),
                        From =
                        [
                            new FromGroupDescription(
                            [
                                new FromLinkDescription("批准", "产生@计划", TriggerConditionDescription.Negative),
                            ]),
                        ],
                        Outputs = [new PortDescription(Result.Value, "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet ContextFlow() => new(
        [],
        [
            new FlowEntry("上下文", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "提供",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("上下文", "Context")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "读取",
                        Kind = "Llm",
                        Inputs = [new InputPortDescription("上下文", "Context"), ModelInput()],
                        Bindings = ModelBinding(),
                        From = [new FromGroupDescription([new FromLinkDescription("上下文", "提供@上下文")])],
                        Outputs = [new PortDescription(Result.Value, "Text")],
                    },
                ],
            }),
        ]);

    private static NodeDefinitionDescription Constant(string name) => new()
    {
        Name = name,
        Kind = "Llm",
        Emission = NodeEmissionDescription.Once,
        Inputs = [ModelInput()],
        Bindings = new Dictionary<string, string>
        {
            [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
        },
        Outputs = [new PortDescription("大纲", "Text", PortModeDescription.Constant)],
    };

    private static Dictionary<string, string> ModelBinding() => new()
    {
        [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
    };

    private static FlowDescriptionSet Signal() => new(
        [],
        [
            new FlowEntry("信号", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "产生",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("计划", "Text")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "放行",
                        Kind = "Llm",
                        Inputs = [new InputPortDescription("批准", "Signal"), ModelInput()],
                        Bindings = ModelBinding(),
                        From = [new FromGroupDescription([new FromLinkDescription("批准", "产生@计划")])],
                        Outputs = [new PortDescription(Result.Value, "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet Literal() => new(
        [],
        [
            new FlowEntry("常量来源", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "提供",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs =
                        [
                            new PortDescription("常量", "Text", PortModeDescription.Constant, Value: "固定值"),
                            new PortDescription("结果", "Text"),
                        ],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "读取",
                        Kind = "Llm",
                        Inputs =
                        [
                            new InputPortDescription(
                                Input.Value,
                                "Text",
                                PortModeDescription.Constant,
                                Role: InputRoleDescription.Content),
                            new InputPortDescription("动态", "Text"),
                            ModelInput(),
                        ],
                        Bindings = new Dictionary<string, string>
                        {
                            [Input.Value] = "提供@常量",
                            [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                        },
                        From = [new FromGroupDescription([new FromLinkDescription("动态", "提供@结果")])],
                        Outputs = [new PortDescription("结论", "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet MultipleEntries() => new(
        [],
        [
            new FlowEntry("多入口", null, [ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [ModelInput()],
                Bindings = ModelBinding(),
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "甲",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                    new NodeDefinitionDescription
                    {
                        Name = "乙",
                        Kind = "Llm",
                        Inputs = [ModelInput()],
                        Bindings = ModelBinding(),
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet ControlInput() => new(
        [],
        [
            new FlowEntry("控制输入", null, [new PortDescription(Goal.Value, "Text"), ModelPort()], new NodeDefinitionDescription
            {
                Name = "整体",
                Kind = "Container",
                Inputs = [new InputPortDescription(Goal.Value, "Text"), ModelInput()],
                Bindings = new Dictionary<string, string>
                {
                    [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                },
                From = [new FromGroupDescription([new FromLinkDescription(Goal.Value, $"@{Goal.Value}")])],
                Nodes =
                [
                    new NodeDefinitionDescription
                    {
                        Name = "甲",
                        Kind = "Llm",
                        Inputs =
                        [
                            new InputPortDescription("控制", "Text", Role: InputRoleDescription.Control),
                            ModelInput(),
                        ],
                        Bindings = ModelBinding(),
                        From = [new FromGroupDescription([new FromLinkDescription("控制", $"@{Goal.Value}")])],
                        Outputs = [new PortDescription("结果", "Text")],
                    },
                ],
            }),
        ]);

    private static FlowDescriptionSet Fixed() => new(
        [],
        [
            new FlowEntry("固定值", null,
                [new PortDescription(WorkPath.Value, "Text", PortModeDescription.Constant, Value: "/tmp/work"), ModelPort()],
                new NodeDefinitionDescription
                {
                    Name = "整体",
                    Kind = "Container",
                    Inputs = [WorkPathInput(), ModelInput()],
                    Bindings = WorkPathBinding(),
                    Nodes =
                    [
                        new NodeDefinitionDescription
                        {
                            Name = "写入",
                            Kind = "Llm",
                            Inputs = [WorkPathInput(), ModelInput()],
                            Bindings = WorkPathBinding(),
                            Outputs = [new PortDescription(Result.Value, "Text")],
                        },
                    ],
                }),
        ]);

    private static InputPortDescription WorkPathInput() =>
        new(WorkPath.Value, "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content);

    private static Dictionary<string, string> WorkPathBinding() => new()
    {
        [WorkPath.Value] = $"@{WorkPath.Value}",
        [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
    };

    private static FlowDescriptionSet ContainerOutput() => new(
        [
            new NodeDefinitionDescription
            {
                Name = "撰写正文",
                Kind = "Llm",
                Emission = NodeEmissionDescription.Once,
                Inputs = [OutlineInput(), ModelInput()],
                Outputs = [new PortDescription("正文", "Text", PortModeDescription.Constant)],
            },
            new NodeDefinitionDescription
            {
                Name = "包装",
                Kind = "Container",
                Inputs = [OutlineInput(), ModelInput()],
                Outputs = [new PortDescription("正文", "Text", PortModeDescription.Constant, Ref: "撰写@正文")],
                Nodes =
                [
                    new NodeReferenceDescription
                    {
                        Label = "撰写",
                        Use = "撰写正文",
                        Bindings = OutlineBinding(),
                    },
                ],
            },
        ],
        [
            new FlowEntry("容器输出", null,
                [new PortDescription(Outline.Value, "Text", PortModeDescription.Constant), ModelPort()],
                new NodeDefinitionDescription
                {
                    Name = "整体",
                    Kind = "Container",
                    Inputs = [OutlineInput(), ModelInput()],
                    Bindings = OutlineBinding(),
                    Nodes =
                    [
                        new NodeReferenceDescription
                        {
                            Label = "成稿",
                            Use = "包装",
                            Bindings = OutlineBinding(),
                        },
                        new NodeDefinitionDescription
                        {
                            Name = "校对",
                            Kind = "Llm",
                            Inputs =
                            [
                                new InputPortDescription("全文", "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content),
                                ModelInput(),
                            ],
                            Bindings = new Dictionary<string, string>
                            {
                                ["全文"] = "成稿@正文",
                                [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
                            },
                            Outputs = [new PortDescription(Result.Value, "Text")],
                        },
                    ],
                }),
        ]);

    private static InputPortDescription OutlineInput() =>
        new(Outline.Value, "Text", PortModeDescription.Constant, Role: InputRoleDescription.Content);

    private static Dictionary<string, string> OutlineBinding() => new()
    {
        [Outline.Value] = $"@{Outline.Value}",
        [MafFlowDefinition.ModelPort.Value] = $"@{MafFlowDefinition.ModelPort.Value}",
    };
}
