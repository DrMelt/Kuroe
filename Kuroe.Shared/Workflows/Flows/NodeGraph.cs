namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程编译出的执行视图：执行节点按先根序存放，依赖以显式边表达，消费方式固化在边上。
/// 执行按“边齐备则激活”推进，执行节点序号只作定位与展示。构造时按入出边建索引。</summary>
public sealed class NodeGraph
{
    private readonly Dictionary<string, int> _byName;
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _incoming = [];
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _outgoing = [];

    /// <summary>构造执行视图，边两端必须落在节点范围内。按入出边建索引。</summary>
    public NodeGraph(IReadOnlyList<ExecutableNode> executableNodes, IReadOnlyList<FlowEdge> edges)
    {
        ExecutableNodes = executableNodes;
        Edges = edges;
        _byName = executableNodes
            .Select((node, index) => (node.Name, index))
            .ToDictionary(entry => entry.Name, entry => entry.index);

        foreach (FlowEdge edge in edges)
        {
            _ = this[edge.From];
            _ = this[edge.To];
            IReadOnlyList<FlowEdge> incoming = _incoming.GetValueOrDefault(edge.To) ?? [];
            IReadOnlyList<FlowEdge> outgoing = _outgoing.GetValueOrDefault(edge.From) ?? [];
            _incoming[edge.To] = [.. incoming, edge];
            _outgoing[edge.From] = [.. outgoing, edge];
        }
    }

    /// <summary>执行节点。</summary>
    public IReadOnlyList<ExecutableNode> ExecutableNodes { get; }

    /// <summary>依赖边。</summary>
    public IReadOnlyList<FlowEdge> Edges { get; }

    /// <summary>执行节点数。</summary>
    public int Count => ExecutableNodes.Count;

    /// <summary>取一个执行节点。</summary>
    public ExecutableNode this[int index] => ExecutableNodes[index];

    /// <summary>按名定位执行节点，不存在时为空。</summary>
    public int? IndexOf(string name) =>
        _byName.TryGetValue(name, out int index) ? index : null;

    /// <summary>目标的入边，按编译顺序。</summary>
    public IReadOnlyList<FlowEdge> Incoming(int target) => _incoming.GetValueOrDefault(target) ?? [];

    /// <summary>来源的出边，按编译顺序。</summary>
    public IReadOnlyList<FlowEdge> Outgoing(int source) => _outgoing.GetValueOrDefault(source) ?? [];

    /// <summary>来源到目标边的消费方式，没有这条边时为空。</summary>
    public EdgeFeed? FeedBetween(int from, int to) =>
        Outgoing(from).FirstOrDefault(edge => edge.To == to)?.Feed;

    /// <summary>按条目展开的依据拆分：入边里 Items 方式的来源，没有时为空。</summary>
    public int? ItemSource(int executableIndex) =>
        Incoming(executableIndex).FirstOrDefault(edge => edge.Feed == EdgeFeed.Items)?.From;

    /// <summary>执行节点的条目空间：实例条目归属的拆分来源。没有 Items 边时沿 Aligned 边递推到源拆分。</summary>
    public int? ItemSpace(int executableIndex) =>
        ItemSource(executableIndex) ?? Incoming(executableIndex)
            .Where(edge => edge.Feed == EdgeFeed.Aligned)
            .Select(edge => ItemSpace(edge.From))
            .FirstOrDefault(space => space is not null);

    /// <summary>检查节点引用的实施来源：入边里非 Plan 输出的来源，整集、逐条与整份一视同仁。</summary>
    public IReadOnlyList<int> CheckedSources(int checkIndex) =>
        [.. Incoming(checkIndex)
            .Select(edge => edge.From)
            .Where(from => this[from].Output != NodeOutput.Plan)
            .Distinct()
            .Order()];
}