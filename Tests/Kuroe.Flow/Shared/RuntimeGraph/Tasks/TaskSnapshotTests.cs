using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
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
            State("制定计划", FlowItemState.Done),
            State("实施", FlowItemState.Done)]).ProgressSteps);
        Assert.Equal(2, Snap([]).TotalSteps);
    }

    [Fact]
    public void Task_without_done_leaves_has_zero_frontier()
    {
        Assert.Equal(0, Snap([State("制定计划", FlowItemState.Running)]).ProgressSteps);
        Assert.Equal(2, Snap([]).TotalSteps);
    }

    [Fact]
    public void Executable_ordinal_follows_the_compiled_order()
    {
        TaskSnapshot snapshot = Snap([]);

        Assert.Equal(1, snapshot.OrdinalOf("制定计划"));
        Assert.Equal(2, snapshot.OrdinalOf("实施"));
        Assert.Equal(3, snapshot.OrdinalOf("Start"));
    }

    private static (string Label, FlowItemState State) State(string label, FlowItemState state) =>
        (label, state);

    private static FlowItemState StateOf(
        string label,
        IReadOnlyList<(string Label, FlowItemState State)> states) =>
        states.FirstOrDefault(state => state.Label == label).State;

    private static TaskSnapshot Snap(IReadOnlyList<(string Label, FlowItemState State)> states) => new(
        new TaskId(1),
        "标题",
        StartInputs.Single(new PortName("目标"), "目标"),
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
        0,
        At,
        At);
}