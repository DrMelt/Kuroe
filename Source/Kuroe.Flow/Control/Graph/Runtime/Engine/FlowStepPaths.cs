using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>流程步骤路径与节点路径之间的转换。</summary>
public static class FlowStepPaths
{
    /// <summary>把节点路径转为流程步骤路径。</summary>
    public static FlowStepPath From(NodePath path) => new([.. path.Levels.Select(level => level.Value)]);

    /// <summary>把流程步骤路径转为节点路径。</summary>
    public static NodePath ToNodePath(FlowStepPath path) => NodePath.Of([.. path.Levels]);
}