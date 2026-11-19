using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;

/// <summary>节点路径与流程步骤路径之间的转换。</summary>
public static class NodeStepPaths
{
    /// <summary>把节点路径转为流程步骤路径。</summary>
    public static FlowStepPath From(NodePath path) =>
        new([.. path.Levels.Select(level => level.Value)]);

    /// <summary>把流程步骤路径转为节点路径。</summary>
    public static NodePath ToNodePath(FlowStepPath path) => NodePath.Of([.. path.Levels]);
}