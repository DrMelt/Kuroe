using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.FlowDescription.Semantics.Tests;

/// <summary>语言解析：来源终点、容器映射、作用域歧义与静态输入约束。</summary>
public sealed class FlowSemanticsAnalyzerTests
{
    private static readonly INodeType ExecutableType = new TestType("Test", new NodeTypeShape
    {
        DispatchesExecutions = true,
    });

    private static readonly INodeType ContainerType = new TestType("Container", new NodeTypeShape
    {
        Organizes = true,
    });

    [Fact(DisplayName = "静态输入解析到装配期常量")]
    public void Static_input_resolves_to_literal_output()
    {
        NodeSpec first = Executable("甲", [ConstantOutput("常量", "固定")], []);
        NodeSpec second = Executable("乙", [], [StaticInput("输入")], Binding("输入", "甲", "常量"));

        FlowSemantics semantics = Analyze(Container(first, second));
        FlowSource source = Input(semantics, "乙", "输入");

        Assert.Equal(FlowSourceKind.NodeConstant, source.Kind);
        Assert.Equal("固定", source.Text);
        Assert.Equal(NodePath.Of("整体", "甲"), source.Node);
    }

    [Fact(DisplayName = "容器输出解析到成员端口")]
    public void Container_output_resolves_to_member_port()
    {
        NodeSpec member = Executable("甲", [DynamicOutput("结果")], []);
        NodeSpec wrapper = new()
        {
            Label = new NodeLabel("包装"),
            Type = ContainerType,
            Outputs =
            [
                new Port(
                    new PortName("对外"),
                    PortTypes.Text,
                    reference: PortRef.Named(new NodeLabel("甲"), new PortName("结果"))),
            ],
            Nodes = [member],
        };

        FlowSemantics semantics = Analyze(Container(wrapper));
        FlowSource source = Output(semantics, "包装", "对外");

        Assert.Equal(FlowSourceKind.NodeOutput, source.Kind);
        Assert.Equal(NodePath.Of("整体", "包装", "甲"), source.Node);
    }

    [Fact(DisplayName = "作用域内重复标签给出诊断")]
    public void Ambiguous_label_reports_diagnostic()
    {
        NodeSpec left = LabeledContainer("左", Executable("同名", [DynamicOutput("结果")], []));
        NodeSpec right = LabeledContainer("右", Executable("同名", [DynamicOutput("结果")], []));
        NodeSpec referrer = Executable("引用", [], [StaticInput("输入")], Binding("输入", "同名", "结果"));

        FlowSemantics semantics = Analyze(Container(left, right, referrer));

        Assert.Contains(semantics.Diagnostics,
            diagnostic => diagnostic.Message.Contains("命中多个节点", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "静态输入取动态来源给出诊断")]
    public void Static_input_from_dynamic_port_reports_diagnostic()
    {
        NodeSpec first = Executable("甲", [DynamicOutput("结果")], []);
        NodeSpec second = Executable("乙", [], [StaticInput("输入")], Binding("输入", "甲", "结果"));

        FlowSemantics semantics = Analyze(Container(first, second));

        Assert.Contains(semantics.Diagnostics,
            diagnostic => diagnostic.Message.Contains("不是常量端口", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "唯一一次执行的常量输出解析为运行期常量")]
    public void Once_emission_constant_resolves_to_runtime_constant()
    {
        NodeSpec first = Executable("甲", [ConstantOutput("常量")], [], emission: NodeEmission.Once);
        NodeSpec second = Executable("乙", [], [StaticInput("输入")], Binding("输入", "甲", "常量"));

        FlowSemantics semantics = Analyze(Container(first, second));
        FlowSource source = Input(semantics, "乙", "输入");

        Assert.Equal(FlowSourceKind.RuntimeConstant, source.Kind);
        Assert.Empty(semantics.Diagnostics);
    }

    [Fact(DisplayName = "开始端口固定值解析为装配常量")]
    public void Fixed_start_port_resolves_to_constant()
    {
        NodeSpec node = Executable("甲", [], [StaticInput("输入")], StartBinding("输入", "目标"));
        FlowSemantics semantics = Analyze(
            Container(node),
            new Port(new PortName("目标"), PortTypes.Text, value: "固定", mode: PortMode.Constant));

        FlowSource source = Input(semantics, "甲", "输入");

        Assert.Equal(FlowSourceKind.StartFixed, source.Kind);
        Assert.Equal("固定", source.Text);
        Assert.Empty(semantics.Diagnostics);
    }

    [Fact(DisplayName = "容器输出缺少来源映射给出诊断")]
    public void Container_output_without_mapping_reports_diagnostic()
    {
        NodeSpec wrapper = new()
        {
            Label = new NodeLabel("包装"),
            Type = ContainerType,
            Outputs = [new Port(new PortName("对外"), PortTypes.Text)],
            Nodes = [Executable("甲", [DynamicOutput("结果")], [])],
        };

        FlowSemantics semantics = Analyze(Container(wrapper));

        Assert.Contains(semantics.Diagnostics,
            diagnostic => diagnostic.Message.Contains("没有来源映射", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "输出端口来源形成环给出诊断")]
    public void Output_source_cycle_reports_diagnostic()
    {
        NodeSpec node = new()
        {
            Label = new NodeLabel("甲"),
            Type = ExecutableType,
            Spec = new ExecutableSpec { Emission = NodeEmission.Once },
            Inputs = [StaticInput("输入")],
            Bindings = new Dictionary<PortName, PortRef>
            {
                [new PortName("输入")] = PortRef.Named(new NodeLabel("甲"), new PortName("常量")),
            },
            Outputs =
            [
                new Port(
                    new PortName("常量"),
                    PortTypes.Text,
                    reference: PortRef.Bound(new PortName("输入")),
                    mode: PortMode.Constant),
            ],
        };

        FlowSemantics semantics = Analyze(Container(node));

        Assert.Contains(semantics.Diagnostics,
            diagnostic => diagnostic.Message.Contains("来源形成环", StringComparison.Ordinal));
    }

    private static FlowSemantics Analyze(NodeSpec root, params Port[] startPorts) =>
        new FlowSemanticsAnalyzer().Analyze(
            new FlowDefinition(new FlowName("语义"), null, startPorts, root));

    private static FlowSource Input(FlowSemantics semantics, string node, string port)
    {
        FlowSemanticNode parsed = Assert.IsType<FlowSemanticNode>(semantics.Find(NodePath.Of("整体", node)));

        return parsed.FindInputs(new PortName(port)).Single().Source;
    }

    private static FlowSource Output(FlowSemantics semantics, string node, string port)
    {
        FlowSemanticNode parsed = Assert.IsType<FlowSemanticNode>(semantics.Find(NodePath.Of("整体", node)));
        FlowSemanticOutput output = Assert.IsType<FlowSemanticOutput>(parsed.FindOutput(new PortName(port)));
        FlowSource? source = output.Source;

        Assert.True(source.HasValue);

        return source.Value;
    }

    private static NodeSpec Container(params NodeSpec[] children) => new()
    {
        Label = new NodeLabel("整体"),
        Type = ContainerType,
        Nodes = children,
    };

    private static NodeSpec LabeledContainer(string label, params NodeSpec[] children) => new()
    {
        Label = new NodeLabel(label),
        Type = ContainerType,
        Nodes = children,
    };

    private static NodeSpec Executable(
        string label,
        IReadOnlyList<Port> outputs,
        IReadOnlyList<InputPort> inputs,
        IReadOnlyDictionary<PortName, PortRef>? bindings = null,
        NodeEmission emission = NodeEmission.Repeated) => new()
        {
            Label = new NodeLabel(label),
            Type = ExecutableType,
            Spec = new ExecutableSpec { Emission = emission },
            Inputs = inputs,
            Bindings = bindings,
            Outputs = outputs,
        };

    private static InputPort StaticInput(string name) =>
        new(new PortName(name), PortTypes.Text, mode: PortMode.Constant);

    private static Dictionary<PortName, PortRef> Binding(string input, string source, string port) =>
        new()
        {
            [new PortName(input)] = PortRef.Named(new NodeLabel(source), new PortName(port)),
        };

    private static Dictionary<PortName, PortRef> StartBinding(string input, string port) =>
        new()
        {
            [new PortName(input)] = PortRef.Start(new PortName(port)),
        };

    private static Port ConstantOutput(string name, string? value = null) =>
        new(new PortName(name), PortTypes.Text, value: value, mode: PortMode.Constant);

    private static Port DynamicOutput(string name) => new(new PortName(name), PortTypes.Text);

    private sealed class TestType(string kind, NodeTypeShape shape) : INodeType
    {
        public NodeKind Kind { get; } = new(kind);

        public NodeTypeShape Shape { get; } = shape;
    }
}