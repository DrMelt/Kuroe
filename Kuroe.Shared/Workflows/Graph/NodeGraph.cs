using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>流程编译出的执行视图：节点按先根序统一编号，开始在最前，执行节点与容器在一张表里，
/// 依赖以显式边表达，边的角色与取值域固化在边上。执行按“组齐备且有驱动则激活”推进，节点序号只作定位与展示。
/// 构造时按入出边建索引、登记容器归属，并算出各节点的输入侧方案。</summary>
public sealed class NodeGraph
{
    private readonly Dictionary<NodeName, int> _byName;
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _incoming = [];
    private readonly Dictionary<int, IReadOnlyList<FlowEdge>> _outgoing = [];
    private readonly Dictionary<int, int> _parentContainer;
    private readonly NodeInputPlan?[] _plans;

    /// <summary>构造执行视图，首个节点必须是开始节点，边两端必须落在节点范围内。按入出边建索引。</summary>
    public NodeGraph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<FlowEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
        Start = nodes[0] as StartNode
            ?? throw new ArgumentException($"节点表的首个节点必须是开始节点，收到 {nodes[0].GetType().Name}。");
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
            _ = this[edge.Source];
            _ = this[edge.Target];
            IReadOnlyList<FlowEdge> incoming = _incoming.GetValueOrDefault(edge.Target) ?? [];
            IReadOnlyList<FlowEdge> outgoing = _outgoing.GetValueOrDefault(edge.Source) ?? [];
            _incoming[edge.Target] = [.. incoming, edge];
            _outgoing[edge.Source] = [.. outgoing, edge];
        }

        _plans = BuildPlans();
    }

    /// <summary>全部节点：开始、执行节点与容器，按先根序统一编号。</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>流程的唯一来源节点，序号 0。</summary>
    public StartNode Start { get; }

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

    /// <summary>来源节点某端口声明的类型，容器与未声明按 Text。供边规则按类型校验。</summary>
    public PortType TypeOf(int nodeIndex, PortName port) =>
        Nodes[nodeIndex] switch
        {
            ExecutableNode executable => FeedRules.TypeOf(executable, port),
            StartNode start => start.FindPort(port)?.Type ?? PortType.Text,
            _ => PortType.Text,
        };

    /// <summary>执行节点的条目空间：实例条目归属的拆分来源。自身取方案的拆分来源，按条目对齐的沿对齐来源递推到源拆分。</summary>
    public int? ItemSpace(int executableIndex) =>
        PlanOf(executableIndex) switch
        {
            { SplitSource: { } split } => split,
            { } plan => plan.AlignedSources.Select(ItemSpace).FirstOrDefault(space => space is not null),
            _ => null,
        };

    /// <summary>节点的输入侧方案，开始节点与输入节点为空。</summary>
    public NodeInputPlan? PlanOf(int index) => _plans[index];

    /// <summary>启动条件只读方案，方案由入边与节点声明一次算出：取值边成组、空间边给实例空间来源、驱动按展开模式定。</summary>
    private NodeInputPlan[] BuildPlans()
    {
        NodeInputPlan[] plans = new NodeInputPlan[Nodes.Count];
        foreach (ExecutableNode executable in ExecutableNodes)
        {
            if (executable.Kind != NodeKind.Input)
            {
                plans[executable.Index] = PlanFor(executable);
            }
        }

        foreach (ContainerNode container in Containers)
        {
            plans[container.Index] = PlanFor(container);
        }

        return plans;
    }

    /// <summary>执行节点的方案：入边按组聚合，空间边不进组，实例空间来源取自空间边或对齐来源。</summary>
    private NodeInputPlan PlanFor(ExecutableNode executable)
    {
        IReadOnlyList<FlowEdge> incoming = Incoming(executable.Index);
        bool byItem = executable.Execution.Mode == NodeMode.PerItem;
        int? splitSource = SplitSource(incoming);
        IReadOnlyList<int> alignedSources =
        [
            .. incoming
                .Where(edge => edge.Kind == EdgeKind.Value && edge.Scope == FeedScope.Item)
                .Select(edge => edge.Source)
                .Distinct(),
        ];

        SpaceKind space = SpaceKind.None;
        if (splitSource is not null)
        {
            space = SpaceKind.Split;
        }
        else if (byItem && alignedSources.Count > 0)
        {
            space = SpaceKind.Aligned;
        }

        return new NodeInputPlan
        {
            Space = space,
            SplitSource = splitSource,
            AlignedSources = alignedSources,
            Branch = executable.Execution.Branch,
            Groups =
            [
                .. incoming
                    .GroupBy(edge => edge.Group)
                    .OrderBy(group => group.Key)
                    .Select(group => new InputGroup([.. group.Select(BindingOf)])),
            ],
            Driver = byItem ? NodeDriver.Completion : NodeDriver.Version,
            MaxRuns = executable.MaxRuns,
        };
    }

    /// <summary>容器的方案：一组绑定覆盖全部直接成员与子容器，成员产出齐备且版本有变化即推进内容版本。</summary>
    private static NodeInputPlan PlanFor(ContainerNode container) => new()
    {
        Space = SpaceKind.None,
        Groups =
        [
            new InputGroup(
            [
                .. container.Members
                    .Concat(container.SubContainers)
                    .Select(member => new InputBinding(EdgeKind.Value, member, FeedScope.Whole, FeedRecurrence.Repeat, false)),
            ]),
        ],
        Driver = NodeDriver.Version,
        MaxRuns = int.MaxValue,
    };

    private static InputBinding BindingOf(FlowEdge edge) =>
        new(edge.Kind, edge.Source, edge.Scope, edge.Recurrence, edge.Type == PortType.Signal);

    /// <summary>入边里提供拆分空间的空间边来源，没有时为空。</summary>
    private static int? SplitSource(IReadOnlyList<FlowEdge> incoming) =>
        incoming.FirstOrDefault(edge => edge.Kind == EdgeKind.Split)?.Source;

    /// <summary>真实依赖环的强连通分量：来源容器展开到全部成员后建邻接，只取成员多于一个或带自连环的分量，成员序号按分量给出。用于环合法性校验。</summary>
    public IReadOnlyList<int[]> Loops()
    {
        ExecutableNode[] executables = [.. ExecutableNodes];
        var rank = new Dictionary<int, int>();
        for (int i = 0; i < executables.Length; i++)
        {
            rank[executables[i].Index] = i;
        }

        var adjacent = new List<List<int>>(executables.Length);
        for (int i = 0; i < executables.Length; i++)
        {
            adjacent.Add([]);
        }

        foreach (FlowEdge edge in Edges)
        {
            foreach (int source in SourcesIn(edge.Source))
            {
                adjacent[rank[source]].Add(rank[edge.Target]);
            }
        }

        var loops = new List<int[]>();
        var index = new int[executables.Length];
        var low = new int[executables.Length];
        Array.Fill(index, -1);
        var stack = new Stack<int>();
        var onStack = new bool[executables.Length];
        int next = 0;

        void Tarjan(int node)
        {
            index[node] = next;
            low[node] = next;
            next++;
            stack.Push(node);
            onStack[node] = true;

            foreach (int successor in adjacent[node])
            {
                if (index[successor] == -1)
                {
                    Tarjan(successor);
                    low[node] = Math.Min(low[node], low[successor]);
                }
                else if (onStack[successor])
                {
                    low[node] = Math.Min(low[node], index[successor]);
                }
            }

            if (low[node] != index[node])
            {
                return;
            }

            List<int> members = [];
            bool selfLoop = false;
            while (true)
            {
                int member = stack.Pop();
                onStack[member] = false;
                members.Add(member);
                selfLoop |= adjacent[member].Contains(member);
                if (member == node)
                {
                    break;
                }
            }

            if (members.Count > 1 || selfLoop)
            {
                loops.Add([.. members.Select(member => executables[member].Index)]);
            }
        }

        for (int start = 0; start < executables.Length; start++)
        {
            if (index[start] == -1)
            {
                Tarjan(start);
            }
        }

        return loops;
    }

    /// <summary>容器及它的全部子容器里的执行节点，递归展开。容器出边的激活消息沿成员到目标的路由边投递。</summary>
    public IReadOnlyList<int> ExecutablesIn(int containerIndex) =>
        [.. SourcesIn(containerIndex).Distinct()];

    /// <summary>来源节点展开出的执行节点集合：执行节点给自身，容器递归给全部成员，开始没有执行节点。</summary>
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
