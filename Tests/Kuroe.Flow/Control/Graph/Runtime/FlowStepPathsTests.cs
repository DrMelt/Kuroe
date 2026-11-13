using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Xunit;

namespace Kuroe.Flow.Runtime.Engine.Tests;

/// <summary>流程步骤路径与节点路径的互相转换。</summary>
public sealed class FlowStepPathsTests
{
    [Fact]
    public void Node_path_round_trips_through_flow_step_path()
    {
        NodePath path = NodePath.Of("整体", "制定计划");

        FlowStepPath step = FlowStepPaths.From(path);

        Assert.Equal(new[] { "整体", "制定计划" }, step.Levels);
        Assert.Equal(path, FlowStepPaths.ToNodePath(step));
    }

    [Fact]
    public void Flow_step_paths_compare_by_levels()
    {
        FlowStepPath first = FlowStepPath.Of("整体", "制定计划");
        FlowStepPath second = new(["整体", "制定计划"]);
        FlowStepPath other = FlowStepPath.Of("整体", "实施");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, other);
    }
}