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

namespace Kuroe.Flow.Tests;

/// <summary>执行输入的端口文本与取值来自同一份截断结果，静态端口文本单独保留。</summary>
public sealed class ExecutionInputBuilderTests
{
    private static readonly PortName Content = new("正文");
    private static readonly PortName Fallback = new("默认");
    private static readonly PortName Static = TestExecutableNodeType.StaticPort;

    [Fact]
    public void Texts_match_the_truncated_inputs()
    {
        string longText = new('甲', 2100);
        string longDefault = new('乙', 2100);
        RuntimeExecutable node = Executable(longDefault);
        NodeInputValues inputs = new(
            [new NodeInputValue(Content, PortTypes.Text, new ContextValue(new StartSource(Content), longText))],
            new HashSet<PortName> { Content },
            staticTexts: new PortTexts(new Dictionary<PortName, string> { [Static] = "静态值" }));

        ExecutionInput input = ExecutionInputBuilder.Build(new TaskId(1), node, inputs, null);

        string expected = new string('甲', 2000) + "…";
        Assert.Equal(expected, input.Text(Content));
        Assert.Equal(expected, input.Inputs.Single(value => value.Port == Content).Text);

        string expectedDefault = new string('乙', 2000) + "…";
        Assert.Equal(expectedDefault, input.Text(Fallback));
        Assert.Equal(expectedDefault, input.Inputs.Single(value => value.Port == Fallback).Text);

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
            new InputPort(Static, PortTypes.Text, isStatic: true),
        ],
        Outputs = [],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    });
}
