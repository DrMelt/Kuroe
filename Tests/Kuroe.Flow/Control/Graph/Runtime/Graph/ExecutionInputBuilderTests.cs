using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;
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
        NodeInputValues inputs = new(
            [new NodeInputValue(Content, PortTypes.Text, new ContextValue(new StartSource(Content), longText))],
            new HashSet<PortName> { Content },
            staticTexts: new PortTexts(new Dictionary<PortName, string> { [Static] = "静态值" }));

        ExecutionInput input = ExecutionInputBuilder.Build(new TaskId(1), node, inputs, null);

        Assert.Equal(longText, input.Text(Content));
        Assert.Equal(longText, input.Inputs.Single(value => value.Port == Content).Text);

        Assert.Equal(longDefault, input.Text(Fallback));
        Assert.Equal(longDefault, input.Inputs.Single(value => value.Port == Fallback).Text);

        Assert.Equal("静态值", input.Text(Static));
        Assert.DoesNotContain(input.Inputs, value => value.Port == Static);
    }

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
