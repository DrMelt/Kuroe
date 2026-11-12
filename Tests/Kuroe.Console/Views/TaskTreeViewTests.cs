using Kuroe.Console.Views;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

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
    public void Tree_prints_the_blocked_reason()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        TaskSnapshot snapshot = Snapshot(cli);
        DateTimeOffset at = At;

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(snapshot with
        {
            ExecutableStates =
            [
                .. snapshot.ExecutableStates.Select(state => state with
                {
                    State = NodeState.Blocked,
                    Block = new NodeBlock("节点失败原因。", null, at),
                }),
            ],
        });

        Assert.Contains("阻塞原因 节点失败原因。", output.Output);
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
                new ExecutableSnapshot(Executable(node), PortTexts.Empty, PortTexts.Empty, [], 0, node.MaxExecutions)),
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
                PortTexts.Empty,
                0,
                container.MaxExecutions)),
        ];

        return new TaskSnapshot(
            new TaskId(1),
            "目标",
            new StartInputs([]),
            PortTexts.Empty,
            View(compiled),
            TaskState.Running,
            0,
            executables,
            states,
            containers,
            [],
            0,
            At,
            At);
    }

    private static FlowExecutableView Executable(ExecutableNode node) => new(
        node.Path,
        node.Label,
        node.Kind,
        node.Type,
        node.Spec,
        node.Inputs,
        node.Outputs,
        node.OutputBindings,
        node.Incoming,
        node.MaxExecutions);

    private static FlowView View(CompiledFlow compiled) => new(
        compiled.Model.Name.Value,
        "graph",
        compiled.Model.Description,
        [],
        [.. compiled.Graph.ExecutableNodes.Select(node => new FlowStepView(
            node.Path.Value,
            node.Label.Value,
            node.Kind.Value,
            NodeState.Pending.ToString(),
            [],
            [],
            [],
            node.MaxExecutions)
        {
            ParentPath = node.Path.Parent?.Value,
        })],
        [.. compiled.Graph.Containers.Select(container => new FlowGroupView(
            container.Path.Value,
            container.Label.Value,
            [.. container.Children.Select(child => child.Path.Value)])
        {
            ParentPath = container.Path.Parent?.Value,
            State = NodeState.Pending.ToString(),
            MaxExecutions = container.MaxExecutions,
        })]);

    /// <summary>固定的快照时间。</summary>
    private static readonly DateTimeOffset At = new(2026, 1, 2, 15, 4, 5, TimeSpan.FromHours(8));
}
