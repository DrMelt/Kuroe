using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Tests.Runtime;

/// <summary>流程步骤路径与节点路径的互相转换。</summary>
public sealed class NodeStepPathsTests
{
    [Fact(DisplayName = "节点路径经流程步骤路径往返")]
    public void Node_path_round_trips_through_flow_step_path()
    {
        NodePath path = NodePath.Of("整体", "制定计划");

        FlowStepPath step = NodeStepPaths.From(path);

        Assert.Equal(["整体", "制定计划"], step.Levels);
        Assert.Equal(path, NodeStepPaths.ToNodePath(step));
    }

    [Fact(DisplayName = "流程步骤路径按层级比较")]
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