using ErrorOr;
using Kuroe.Flow.Compilation.Graph;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Compilation.Validation;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

/// <summary>静态端口链路：展开保留静态输出，容器静态输出取成员静态输入，开始端口绑定保留为来源，错误来源与 From 连接静态端口被拒绝。</summary>
public sealed class StaticPortTests
{
    private static readonly PortName Goal = new("目标");
    private static readonly PortName Plan = new("计划");
    private static readonly PortName Result = new("结论");
    private static readonly PortName Model = TestExecutableNodeType.StaticPort;

    /// <summary>容器静态输出转发成员静态输入，下游节点运行期解析出开始端口的值。</summary>
    [Fact]
    public void Container_static_output_forwards_member_static_input()
    {
        NodeSpec library = DeliveryDefinition();
        NodeSpec executable = ExecutionDefinition();
        FlowDefinition flow = FlowWithStaticOutput();

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], [library, executable]);
        Assert.False(expanded.IsError,
            string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));
        FlowDefinition tree = Assert.Single(expanded.Value);

        NodeSpec delivery = tree.RootNode.Nodes!.Single(node => node.Label.Value == "交付");
        Assert.True(delivery.ContainerOutputs![Model].IsStatic);

        ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(tree);
        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        ExecutableNode check = compiled.Value.ExecutableNodes.Single(node => node.Label.Value == "检查");
        Assert.Equal("fake", StaticText(compiled.Value, check, Model));
    }

    /// <summary>静态端口视图只含静态输入，动态输入不进入视图。</summary>
    [Fact]
    public void Static_view_contains_only_static_inputs()
    {
        NodeSpec library = DeliveryDefinition();
        NodeSpec executable = ExecutionDefinition();
        FlowDefinition flow = FlowWithStaticOutput();

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], [library, executable]);
        Assert.False(expanded.IsError,
            string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(Assert.Single(expanded.Value));
        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        ExecutableNode check = compiled.Value.ExecutableNodes.Single(node => node.Label.Value == "检查");
        IPortTexts view = new StaticPortValues(compiled.Value, PortTexts.Empty).For(check);

        Assert.Equal("fake", view.Text(Model));
        Assert.Null(view.Text(Goal));
    }

    /// <summary>From 连接静态输入端口报出专用错误。</summary>
    [Fact]
    public void From_cannot_connect_a_static_input()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, null, "fake", IsStatic: true)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
                In = RootBindings(),
                Nodes =
                [
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
                        Static = new Dictionary<PortName, PortRef> { [Model] = PortRef.Named(StartNode.ReservedLabel, Model) },
                        Spec = new ExecutableSpec(),
                        From =
                        [
                            new FromGroup([
                                new FromLink(Goal, PortRef.Bound(Goal)),
                                new FromLink(Model, PortRef.Bound(Model)),
                            ]),
                        ],
                    },
                ],
            });

        Assert.Contains(Errors(flow), error => error.Contains($"From 不能连接静态输入端口 {Model}"));
    }

    /// <summary>静态来源指向执行节点的动态输出时报错。</summary>
    [Fact]
    public void Static_source_cannot_be_an_execution_output()
    {
        NodeSpec source = NodeSpec.Define(new NodeName("来源")) with
        {
            Type = TestNodeTypes.Executable,
            Inputs = [new InputPort(Goal)],
            Outputs = [new Port(Result, PortTypes.Text)],
            Spec = new ExecutableSpec(),
            From = [new FromGroup([new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Goal))])],
        };

        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, null, "fake", IsStatic: true)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
                In = RootBindings(),
                Nodes =
                [
                    source,
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Static = new Dictionary<PortName, PortRef>
                        {
                            [Model] = PortRef.Named(new NodeLabel("来源"), Result),
                        },
                        Spec = new ExecutableSpec(),
                        From = [new FromGroup([new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Goal))])],
                    },
                ],
            });

        Assert.Contains(Errors(flow), error => error.Contains("输出端口 结论 不是静态端口"));
    }

    /// <summary>静态开始端口没有 Value 也没有宿主输入绑定时报错。</summary>
    [Fact]
    public void Static_start_port_requires_a_value_or_host_binding()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, IsStatic: true)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
                In = RootBindings(),
                Nodes =
                [
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Static = new Dictionary<PortName, PortRef> { [Model] = PortRef.Named(StartNode.ReservedLabel, Model) },
                        Spec = new ExecutableSpec(),
                        From = [new FromGroup([new FromLink(Goal, PortRef.Bound(Goal))])],
                    },
                ],
            });

        Assert.Contains(Errors(flow), error => error.Contains($"静态开始端口 {Model} 必须有 Value 或宿主输入绑定"));
    }

    /// <summary>静态开始端口绑定宿主输入时保留为来源绑定，取值在任务启动后由注入的端口文本给出。</summary>
    [Fact]
    public void Externally_bound_static_start_port_keeps_a_binding()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, IsStatic: true)],
            new Dictionary<PortName, string> { [Model] = "MODEL" },
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
                In = RootBindings(),
                Nodes =
                [
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Static = new Dictionary<PortName, PortRef> { [Model] = PortRef.Bound(Model) },
                        Spec = new ExecutableSpec(),
                        From = [new FromGroup([new FromLink(Goal, PortRef.Bound(Goal))])],
                    },
                ],
            });

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], []);
        Assert.False(expanded.IsError,
            string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(Assert.Single(expanded.Value));
        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        ExecutableNode check = compiled.Value.ExecutableNodes.Single(node => node.Label.Value == "检查");
        Assert.Equal(new NodePort(new NodePath([StartNode.ReservedLabel]), Model), check.StaticBindings![Model]);
        Assert.Equal("fake", StaticText(
            compiled.Value,
            check,
            Model,
            new PortTexts(new Dictionary<PortName, string> { [Model] = "fake" })));
        Assert.Equal("other", StaticText(
            compiled.Value,
            check,
            Model,
            new PortTexts(new Dictionary<PortName, string> { [Model] = "other" })));
    }

    /// <summary>静态开始端口不进入 From，即使它绑定了宿主输入。</summary>
    [Fact]
    public void Static_start_port_cannot_feed_from()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, null, "fake", IsStatic: true)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("检查")) with
            {
                Type = TestNodeTypes.Executable,
                Inputs = [new InputPort(Goal)],
                Spec = new ExecutableSpec(),
                From = [new FromGroup([new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Model))])],
            });

        Assert.Contains(Errors(flow), error => error.Contains($"静态开始端口 {Model} 不进入 From"));
    }

    /// <summary>开始端口只写 Value 不写 Static 时仍不是静态端口，静态绑定读取时报错。</summary>
    [Fact]
    public void Value_alone_does_not_make_a_static_start_port()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, null, "fake")],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal), new InputPort(Model)],
                In = RootBindings(),
                Nodes =
                [
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Static = new Dictionary<PortName, PortRef> { [Model] = PortRef.Bound(Model) },
                        Spec = new ExecutableSpec(),
                        From = [new FromGroup([new FromLink(Goal, PortRef.Bound(Goal))])],
                    },
                ],
            });

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], []);
        Assert.False(expanded.IsError,
            string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        Assert.Contains(Errors(Assert.Single(expanded.Value)),
            error => error.Contains($"开始端口 {Model} 不是静态端口"));
    }

    /// <summary>静态绑定形成环时编译报错。</summary>
    [Fact]
    public void Static_binding_cycle_is_rejected()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Nodes =
                [
                    NodeSpec.Define(new NodeName("交付")) with
                    {
                        Type = ContainerNodeTypes.Type,
                        ContainerOutputs = new Dictionary<PortName, ContainerOutput>
                        {
                            [Model] = new ContainerOutput(PortRef.Named(new NodeLabel("检查"), Model), IsStatic: true),
                        },
                        Nodes =
                        [
                            NodeSpec.Define(new NodeName("检查")) with
                            {
                                Type = TestNodeTypes.Executable,
                                Inputs = [new InputPort(Goal)],
                                Static = new Dictionary<PortName, PortRef>
                                {
                                    [Model] = PortRef.Named(new NodeLabel("交付"), Model),
                                },
                                Spec = new ExecutableSpec(),
                                From =
                                [
                                    new FromGroup([
                                        new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Goal)),
                                    ]),
                                ],
                            },
                        ],
                    },
                ],
            });

        Assert.Contains(Errors(flow), error => error.Contains($"静态端口 {Model} 形成环"));
    }

    /// <summary>容器静态输出绑定执行节点的动态输出时报错。</summary>
    [Fact]
    public void Container_static_output_must_bind_a_static_port()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = ContainerNodeTypes.Type,
                Inputs = [new InputPort(Goal)],
                ContainerOutputs = new Dictionary<PortName, ContainerOutput>
                {
                    [Model] = new ContainerOutput(PortRef.Named(new NodeLabel("目标"), Result), IsStatic: true),
                },
                Nodes =
                [
                    NodeSpec.Define(new NodeName("目标")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Outputs = [new Port(Result, PortTypes.Text)],
                        Spec = new ExecutableSpec(),
                        From =
                        [
                            new FromGroup([
                                new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Goal)),
                            ]),
                        ],
                    },
                ],
            });

        Assert.Contains(Errors(flow), error => error.Contains("不是静态端口"));
    }

    /// <summary>容器类型声明的静态输入必须由引用处绑定。</summary>
    [Fact]
    public void Container_static_input_requires_an_in_binding()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("整体")) with
            {
                Type = new StaticContainerType(),
                Inputs = [new InputPort(Goal)],
                In = new Dictionary<PortName, PortRef>
                {
                    [Goal] = PortRef.Named(StartNode.ReservedLabel, Goal),
                },
                Nodes =
                [
                    NodeSpec.Define(new NodeName("检查")) with
                    {
                        Type = TestNodeTypes.Executable,
                        Inputs = [new InputPort(Goal)],
                        Spec = new ExecutableSpec(),
                        From = [new FromGroup([new FromLink(Goal, PortRef.Bound(Goal))])],
                    },
                ],
            });

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander()
            .Expand([flow], [], []);
        Assert.True(expanded.IsError);
        Assert.Contains(expanded.ErrorsOrEmptyList,
            error => error.Description.Contains($"端口 {Model} 没有绑定"));
    }

    /// <summary>执行节点静态输入的默认值在读取端口时解析。</summary>
    [Fact]
    public void Static_input_default_resolves_at_access()
    {
        FlowDefinition flow = new(new FlowName("默认"), null,
            [new Port(Goal, PortTypes.Text)],
            new Dictionary<PortName, string>(),
            NodeSpec.Define(new NodeName("检查")) with
            {
                Type = TestNodeTypes.Executable,
                Inputs = [new InputPort(Goal)],
                Spec = new ExecutableSpec(),
                From =
                [
                    new FromGroup([
                        new FromLink(Goal, PortRef.Named(StartNode.ReservedLabel, Goal)),
                    ]),
                ],
            });

        ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(flow);
        Assert.False(compiled.IsError,
            string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));

        ExecutableNode check = compiled.Value.ExecutableNodes.Single(node => node.Label.Value == "检查");
        Assert.Null(check.StaticBindings);
        Assert.Equal("执行者", StaticText(compiled.Value, check, TestExecutableNodeType.StaticPort));
    }

    /// <summary>按编译视图与注入的开始端口文本取静态端口值。</summary>
    private static string? StaticText(
        NodeGraph graph,
        ExecutableNode node,
        PortName port,
        IPortTexts? startTexts = null) =>
        new StaticPortValues(graph, startTexts ?? PortTexts.Empty).For(node).Text(port);

    private sealed class StaticContainerType : INodeType, INodeTypeValidation
    {
        public NodeKind Kind { get; } = new("StaticContainer");

        public NodeTypeShape Shape { get; } = new()
        {
            Organizes = true,
            InputTypes = [PortTypes.Text],
            StaticInputs =
            [
                new InputPort(TestExecutableNodeType.StaticPort, PortTypes.Text, isStatic: true),
            ],
        };

        public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
        {
        }
    }

    private static NodeSpec ExecutionDefinition() => NodeSpec.Define(new NodeName("执行")) with
    {
        Type = TestNodeTypes.Executable,
        Inputs = [new InputPort(Plan)],
        Outputs = [new Port(Result, PortTypes.Text)],
        Spec = new ExecutableSpec(),
    };

    private static NodeSpec DeliveryDefinition() => NodeSpec.Define(new NodeName("交付")) with
    {
        Type = ContainerNodeTypes.Type,
        Inputs = [new InputPort(Plan), new InputPort(Model, PortTypes.Text, isStatic: true)],
        ContainerOutputs = new Dictionary<PortName, ContainerOutput>
        {
            [Model] = new ContainerOutput(PortRef.Named(new NodeLabel("实施"), Model), IsStatic: true),
        },
        Nodes =
        [
            NodeSpec.Reference(new NodeLabel("实施"), new NodeName("执行")) with
            {
                Static = new Dictionary<PortName, PortRef> { [Model] = PortRef.Bound(Model) },
                From = [new FromGroup([new FromLink(Plan, PortRef.Bound(Plan))])],
            },
        ],
    };

    private static FlowDefinition FlowWithStaticOutput() => new(
        new FlowName("默认"),
        null,
        [new Port(Goal, PortTypes.Text), new Port(Model, PortTypes.Text, null, "fake", IsStatic: true)],
        new Dictionary<PortName, string>(),
        NodeSpec.Define(new NodeName("整体")) with
        {
            Type = ContainerNodeTypes.Type,
            Inputs = [new InputPort(Goal), new InputPort(Model, PortTypes.Text, isStatic: true)],
            In = RootBindings(),
            Nodes =
            [
                NodeSpec.Reference(new NodeLabel("交付"), new NodeName("交付")) with
                {
                    In = new Dictionary<PortName, PortRef>
                    {
                        [Plan] = PortRef.Bound(Goal),
                        [Model] = PortRef.Bound(Model),
                    },
                },
                NodeSpec.Define(new NodeName("检查")) with
                {
                    Type = TestNodeTypes.Executable,
                    Inputs = [new InputPort(Goal)],
                    Static = new Dictionary<PortName, PortRef>
                    {
                        [Model] = PortRef.Named(new NodeLabel("交付"), Model),
                    },
                    Spec = new ExecutableSpec(),
                    From = [new FromGroup([new FromLink(Goal, PortRef.Bound(Goal))])],
                },
            ],
        });

    private static Dictionary<PortName, PortRef> RootBindings() => new()
    {
        [Goal] = PortRef.Named(StartNode.ReservedLabel, Goal),
        [Model] = PortRef.Named(StartNode.ReservedLabel, Model),
    };

    private static IReadOnlyList<string> Errors(FlowDefinition flow) =>
        [.. FlowRules.ValidateAndCompile(flow).ErrorsOrEmptyList.Select(error => error.Description)];
}