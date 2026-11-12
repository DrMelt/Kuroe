using Kuroe.Console.Views;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;
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
        FlowTemplateView flow = cli.Environment.Templates.Find(new FlowName("默认")).ThrowIfError();

        cli.Terminal.Write(NodeTree.Build(
            flow,
            new ViewLine().Add("节点", Styles.Hint).Build(),
            group => new ViewLine().Add(group.Label, Styles.Key).Build(),
            step => new ViewLine().Add(step.Label, Styles.Key).Build(),
            group => [new Text("详情")]));

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