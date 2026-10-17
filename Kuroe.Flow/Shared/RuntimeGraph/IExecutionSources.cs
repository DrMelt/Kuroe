using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定读的来源侧：节点状态与已交回的拆分。节点按路径定位。</summary>
public interface IExecutionSources
{
    /// <summary>节点路径对应的节点状态，未实例化的节点给未放行的默认状态。</summary>
    ISourceState Source(NodePath node);

    /// <summary>某执行节点是否已交回条目拆分。</summary>
    bool HasSplit(NodePath node);

    /// <summary>某执行节点交回的条目序，按分支过滤；尚未交回为空。</summary>
    IReadOnlyList<int>? SplitItems(NodePath node, BranchName? branch);

    /// <summary>某执行节点已展开的实例集，尚未展开与整节点、容器为空。</summary>
    IReadOnlyList<int>? Items(NodePath node);
}
