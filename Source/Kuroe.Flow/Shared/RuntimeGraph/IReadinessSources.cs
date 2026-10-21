using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定读的来源侧：节点状态。节点按路径定位。</summary>
public interface IReadinessSources
{
    /// <summary>节点路径对应的节点状态，未实例化的节点给未放行的默认状态。</summary>
    ISourceState Source(NodePath node);
}
