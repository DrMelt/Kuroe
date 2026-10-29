using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>执行输入的端口文本：已装配的文本表是唯一读取来源。</summary>
public sealed class ExecutionInputTests
{
    private static readonly PortName Content = new("正文");

    [Fact]
    public void Text_reads_the_assembled_table()
    {
        ExecutionInput input = Input(new Dictionary<PortName, string> { [Content] = "内容" });

        Assert.Equal("内容", input.Text(Content));
    }

    [Fact]
    public void Undeclared_port_has_no_text()
    {
        ExecutionInput input = Input(new Dictionary<PortName, string>());

        Assert.Null(input.Text(new PortName("未装配")));
    }

    [Fact]
    public void Raw_inputs_do_not_leak_through_the_text_table()
    {
        ExecutionInput input = Input(
            new Dictionary<PortName, string>(),
            new InputValue(Content, new ContextValue(new StartSource(Content), "动态")));

        Assert.Null(input.Text(Content));
    }

    private static ExecutionInput Input(
        IReadOnlyDictionary<PortName, string> texts,
        params InputValue[] inputs) => new()
        {
            Task = new TaskId(1),
            Node = NodePath.Of("节点"),
            Type = new FakeNodeType(),
            Inputs = inputs,
            Texts = texts,
        };

    private sealed class FakeNodeType : INodeType
    {
        public NodeKind Kind { get; } = new("Fake");

        public NodeTypeShape Shape { get; } = new();
    }
}