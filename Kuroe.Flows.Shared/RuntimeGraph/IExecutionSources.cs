using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.Shared.RuntimeGraph;

/// <summary>判定读的来源侧：节点状态与已交回的拆分。</summary>
public interface IExecutionSources
{
    /// <summary>序号对应的节点状态，未实例化的节点给未放行的默认状态。</summary>
    ISourceState Source(int nodeIndex);

    /// <summary>某执行节点是否已交回条目拆分。</summary>
    bool HasSplit(int nodeIndex);

    /// <summary>某执行节点交回的条目序，按分支过滤；尚未交回为空。</summary>
    IReadOnlyList<int>? SplitItems(int nodeIndex, BranchName? branch);

    /// <summary>某执行节点已展开的实例集，尚未展开与整节点、容器为空。</summary>
    IReadOnlyList<int>? Items(int nodeIndex);
}
