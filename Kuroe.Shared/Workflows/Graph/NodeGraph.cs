using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>流程编译出的执行视图：节点按先根序统一编号，执行节点与容器在一张表里，
/// 依赖以显式边表达，消费方式固化在边上。执行按“边齐备则激活”推进，节点序号只作定位与展示。
/// 构造时按入出边建索引并登记容器归属。</summary>
public sealed class NodeGraph
{
    private readonly Dictionary<NodeName, int> _byName;
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _incoming = [];
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _outgoing = [];
    private readonly Dictionary<int, int> _parentContainer;

    /// <summary>构造执行视图，边两端必须落在节点范围内。按入出边建索引。</summary>
    public NodeGraph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<FlowEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
        ExecutableNodes = [.. nodes.OfType<ExecutableNode>()];
        Containers = [.. nodes.OfType<ContainerNode>()];
        _byName = nodes.ToDictionary(node => node.Name, node => node.Index);

        _parentContainer = [];
        foreach (ContainerNode container in Containers)
        {
            foreach (int member in container.Members)
            {
                _parentContainer[member] = container.Index;
            }

            foreach (int child in container.SubContainers)
            {
                _parentContainer[child] = container.Index;
            }
        }

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

    /// <summary>全部节点：执行节点与容器，按先根序统一编号。</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>执行节点，按先根序。</summary>
    public IReadOnlyList<ExecutableNode> ExecutableNodes { get; }

    /// <summary>容器节点，按先根序。</summary>
    public IReadOnlyList<ContainerNode> Containers { get; }

    /// <summary>依赖边。</summary>
    public IReadOnlyList<FlowEdge> Edges { get; }

    /// <summary>节点数，执行节点与容器一起算。</summary>
    public int Count => Nodes.Count;

    /// <summary>执行节点数。</summary>
    public int TotalExecutables => ExecutableNodes.Count;

    /// <summary>取一个节点。</summary>
    public GraphNode this[int index] => Nodes[index];

    /// <summary>按名定位节点，不存在时为空。</summary>
    public int? IndexOf(NodeName name) =>
        _byName.TryGetValue(name, out int index) ? index : null;

    /// <summary>节点直接所属的容器，根级节点为空。</summary>
    public ContainerNode? ContainerOf(int nodeIndex) =>
        _parentContainer.TryGetValue(nodeIndex, out int container) ? (ContainerNode)Nodes[container] : null;

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

    /// <summary>检查节点引用的实施来源：入边来源展开出的全部非 Plan 执行节点，整集、逐条与整份一视同仁。
    /// 执行节点给自身，容器递归给全部成员，据此从容器汇合引用到容器内实施。</summary>
    public IReadOnlyList<int> CheckedSources(int checkIndex) =>
        [.. Incoming(checkIndex)
            .SelectMany(edge => SourcesIn(edge.From))
            .Where(from => Nodes[from] is ExecutableNode { Output: not NodeOutput.Plan })
            .Distinct()
            .Order()];

    /// <summary>容器及它的全部子容器里的执行节点，递归展开。容器出边的激活消息沿成员到目标的路由边投递。</summary>
    public IReadOnlyList<int> ExecutablesIn(int containerIndex) =>
        [.. SourcesIn(containerIndex).Distinct()];

    /// <summary>来源节点展开出的执行节点集合：执行节点给自身，容器递归给全部成员。</summary>
    private IEnumerable<int> SourcesIn(int nodeIndex) =>
        Nodes[nodeIndex] switch
        {
            ExecutableNode _ => [nodeIndex],
            ContainerNode container => container.Members
                .Concat(container.SubContainers)
                .SelectMany(member => SourcesIn(member)),
            _ => [],
        };
}