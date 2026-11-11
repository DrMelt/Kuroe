using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Graph;

/// <summary>执行输入的端口文本与取值来自同一份完整取值，静态端口文本单独保留。</summary>
public sealed class ExecutionInputBuilderTests
{
    private static readonly PortName Content = new("正文");
    private static readonly PortName Fallback = new("默认");
    private static readonly PortName Static = TestExecutableNodeType.StaticPort;

    [Fact]
    public void Texts_match_the_full_inputs()
    {
        string longText = new('甲', 2100);
        string longDefault = new('乙', 2100);
        RuntimeExecutable node = Executable(longDefault);
        NodeGraph graph = new([], [node.Executable], []);
        NodeInputValues inputs = new(
            [new NodeInputValue(Content, PortTypes.Text, new ContextValue(new StartSource(Content), longText))],
            new HashSet<PortName> { Content },
            staticTexts: new PortTexts(new Dictionary<PortName, string> { [Static] = "静态值" }));

        ExecutionInput input = ExecutionInputBuilder.Build(new TaskId(1), node, inputs, null, graph);

        Assert.Equal(longText, input.Text(Content));
        Assert.Equal(longText, input.Inputs.Single(value => value.Port == Content).Text);

        Assert.Equal(longDefault, input.Text(Fallback));
        Assert.Equal(longDefault, input.Inputs.Single(value => value.Port == Fallback).Text);

        Assert.Equal("静态值", input.Text(Static));
        Assert.DoesNotContain(input.Inputs, value => value.Port == Static);
    }

    [Fact]
    public void Static_content_input_reports_its_start_port()
    {
        PortName start = new("来源");
        PortName target = new("目标");
        ExecutableNode executable = StaticExecutable(
            new InputPort(target, PortTypes.Text, mode: PortMode.Constant, role: InputRole.Content),
            new InputBinding(NodePath.Of("节点"), BindingSource.ForStart(start), target, Role: BindingRole.Static));
        NodeGraph graph = new([new Port(start, PortTypes.Text, mode: PortMode.Constant)], [executable], []);

        InputValue value = Build(executable, graph, target, "来源值");

        Assert.Equal(target, value.Port);
        Assert.Equal("来源值", value.Text);
        Assert.Equal(start, Assert.IsType<StartSource>(value.Origin).Port);
    }

    [Fact]
    public void Static_content_input_reports_its_constant_source()
    {
        PortName output = new("要求");
        PortName target = new("目标");
        ExecutableNode upstream = new()
        {
            Path = NodePath.Of("上游"),
            Type = TestNodeTypes.Executable,
            Spec = new ExecutableSpec(),
            Outputs = [new Port(output, PortTypes.Text, mode: PortMode.Constant, value: "固定值")],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
        };
        ExecutableNode executable = StaticExecutable(
            new InputPort(target, PortTypes.Text, mode: PortMode.Constant, role: InputRole.Content),
            new InputBinding(NodePath.Of("节点"), BindingSource.ForNode(upstream.Path, output), target, Role: BindingRole.Static));
        NodeGraph graph = new([], [upstream, executable], []);

        InputValue value = Build(executable, graph, target, "固定值");

        ConstantSource source = Assert.IsType<ConstantSource>(value.Origin);
        Assert.Equal(upstream.Path, source.Node);
        Assert.Equal(output, source.Port);
    }

    [Fact]
    public void Static_content_input_reports_its_default()
    {
        PortName target = new("目标");
        ExecutableNode executable = StaticExecutable(
            new InputPort(target, PortTypes.Text, defaultValue: "兜底", mode: PortMode.Constant, role: InputRole.Content));
        NodeGraph graph = new([], [executable], []);

        InputValue value = Build(executable, graph, target, "兜底");

        DefaultSource source = Assert.IsType<DefaultSource>(value.Origin);
        Assert.Equal(executable.Path, source.Node);
        Assert.Equal(target, source.Port);
    }

    private static InputValue Build(ExecutableNode executable, NodeGraph graph, PortName port, string text)
    {
        RuntimeExecutable node = new(executable);
        NodeInputValues inputs = new(
            [],
            new HashSet<PortName>(),
            staticTexts: new PortTexts(new Dictionary<PortName, string> { [port] = text }));

        ExecutionInput input = ExecutionInputBuilder.Build(new TaskId(1), node, inputs, null, graph);

        return Assert.Single(input.Inputs);
    }

    private static ExecutableNode StaticExecutable(InputPort input, InputBinding? binding = null) => new()
    {
        Path = NodePath.Of("节点"),
        Type = TestNodeTypes.Executable,
        Spec = new ExecutableSpec(),
        Inputs = [input],
        InputBindings = binding is { } value ? new Dictionary<PortName, InputBinding> { [input.Name] = value } : null,
        Outputs = [],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    };

    private static RuntimeExecutable Executable(string fallback) => new(new ExecutableNode
    {
        Path = NodePath.Of("节点"),
        Type = TestNodeTypes.Executable,
        Spec = new ExecutableSpec(),
        Inputs =
        [
            new InputPort(Content),
            new InputPort(Fallback, PortTypes.Text, defaultValue: fallback),
            new InputPort(Static, PortTypes.Text, mode: PortMode.Constant),
        ],
        Outputs = [],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    });
}
