using Kuroe.Console.Views;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>任务树的渲染：直接构造任务快照，不提交任务。</summary>
public sealed class TaskTreeViewTests
{
    [Fact]
    public void Tree_prints_only_the_node_tree()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(Snapshot(cli));

        Assert.Contains("节点", output.Output);
        Assert.Contains("整体", output.Output);
        Assert.Contains("制定计划", output.Output);
        Assert.DoesNotContain("过程记录", output.Output);
        Assert.DoesNotContain('┌', output.Output);
    }

    [Fact]
    public void Tree_reads_the_node_state_from_the_snapshot()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        TaskSnapshot snapshot = Snapshot(cli);

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(snapshot with
        {
            ExecutableStates = [.. snapshot.ExecutableStates.Select(state => state with { State = NodeState.Done })],
        });

        Assert.Contains("已完成", output.Output);
    }

    private static TaskSnapshot Snapshot(ConsoleSession cli)
    {
        CompiledFlow compiled = cli.Environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError();
        IReadOnlyList<ExecutableSnapshot> executables =
        [
            .. compiled.Graph.ExecutableNodes.Select(node =>
                new ExecutableSnapshot(node, PortTexts.Empty, PortTexts.Empty, [])),
        ];
        IReadOnlyList<ExecutableStateSnapshot> states =
        [
            .. compiled.Graph.ExecutableNodes.Select(node => new ExecutableStateSnapshot(node.Path, NodeState.Pending)),
        ];
        IReadOnlyList<ContainerSnapshot> containers =
        [
            .. compiled.Graph.Containers.Select(container => new ContainerSnapshot(
                container.Path,
                NodeState.Pending,
                [.. container.Children.Select(child => child.Path)],
                PortTexts.Empty)),
        ];

        return new TaskSnapshot(
            new TaskId(1),
            "目标",
            new StartInputs([]),
            PortTexts.Empty,
            compiled.Model,
            compiled.Graph,
            TaskState.Running,
            0,
            executables,
            states,
            containers,
            [],
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
