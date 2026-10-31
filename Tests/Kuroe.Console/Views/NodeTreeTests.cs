using Kuroe.Console.Views;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>节点树与行组合的渲染。</summary>
public sealed class NodeTreeTests
{
    [Fact]
    public void View_line_skips_empty_parts_and_separates_facts()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Write(new ViewLine()
            .Add("节点", Styles.Key)
            .Add(string.Empty, Styles.Hint)
            .Add("已完成", Styles.Success)
            .Build());

        Assert.Contains("节点 · 已完成", output.Output);
    }

    [Fact]
    public void Node_tree_renders_hierarchy_and_details()
    {
        using ConsoleSession cli = new();
        CompiledFlow flow = cli.Environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError();

        cli.Terminal.Write(NodeTree.Build(
            flow.Graph,
            new ViewLine().Add("节点", Styles.Hint).Build(),
            node => new ViewLine().Add(node.Label.Value, Styles.Key).Build(),
            node => node is ExecutableNode ? [new Text("详情")] : []));

        string[] lines = cli.Output.Output.Replace("\r", string.Empty).Split('\n');
        int container = Array.FindIndex(lines, line => line.Contains("整体"));
        int nested = Array.FindIndex(lines, line => line.Contains("分配执行"));

        Assert.Contains("├", cli.Output.Output);
        Assert.Contains("└", cli.Output.Output);
        Assert.Contains("详情", cli.Output.Output);
        Assert.Contains("分配执行", cli.Output.Output);
        Assert.EndsWith("\n", cli.Output.Output);
        Assert.True(container >= 0);
        Assert.True(nested > container, cli.Output.Output);
    }
}