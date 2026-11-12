using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Tasks;

/// <summary>任务快照的派生属性：任务进度与执行节点顺位。</summary>
public sealed class TaskSnapshotTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Task_frontier_comes_from_the_furthest_done_leaf()
    {
        Assert.Equal(2, Snap([
            State(NodePath.Of("制定计划"), NodeState.Done),
            State(NodePath.Of("实施"), NodeState.Done)]).FrontierNodes);
        Assert.Equal(2, Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Task_without_done_leaves_has_zero_frontier()
    {
        Assert.Equal(0, Snap([State(NodePath.Of("制定计划"), NodeState.Running)]).FrontierNodes);
        Assert.Equal(2, Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Executable_ordinal_follows_the_compiled_order()
    {
        TaskSnapshot snapshot = Snap([]);

        Assert.Equal(1, snapshot.OrdinalOf(NodePath.Of("制定计划")));
        Assert.Equal(2, snapshot.OrdinalOf(NodePath.Of("实施")));
        Assert.Equal(3, snapshot.OrdinalOf(NodePath.Of("Start")));
    }

    private static ExecutableStateSnapshot State(NodePath node, NodeState state) => new(node, state);

    private static string StateOf(string label, IReadOnlyList<ExecutableStateSnapshot> states) =>
        states.FirstOrDefault(state => state.Node.Label.Value == label)?.State.ToString() ?? "Pending";

    private static TaskSnapshot Snap(IReadOnlyList<ExecutableStateSnapshot> states) => new(
        new TaskId(1),
        "标题",
        StartInputs.Single(new PortName("目标"), "目标"),
        PortTexts.Empty,
        new FlowView(
            "默认",
            "graph",
            null,
            [],
            [
                new FlowStepView("制定计划", "制定计划", "Executable", StateOf("制定计划", states), [], [], [], 100),
                new FlowStepView("实施", "实施", "Executable", StateOf("实施", states), [], [], [], 100),
            ],
            []),
        TaskState.Running,
        0,
        [],
        states,
        [],
        [],
        0,
        At,
        At);
}