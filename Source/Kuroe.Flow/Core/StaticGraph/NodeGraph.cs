using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.StaticGraph;

/// <summary>流程编译出的执行视图：节点以从根到本节点的路径标识，开始是固定来源，执行节点与容器在同一棵树上，
/// 依赖以显式边表达，边的角色与取值域固化在边上。执行按“组齐备且有驱动则激活”推进。
/// 树与边在编译期落定，与节点相关的边在构造时挂到节点上，输入侧方案由节点按自身入边算出，读取按路径逐段下钻。</summary>
public sealed class NodeGraph : INodeGraph
{
    private readonly IReadOnlyList<GraphNode> _roots;

    /// <summary>构造执行视图：首个节点必须是开始，节点按路径落在同一棵树上，同层节点标签必须唯一，边两端必须落在节点表里。
    /// 与节点相关的边在构造时挂到节点上：出边落在来源，入边落在执行节点。</summary>
    public NodeGraph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<FlowEdge> edges)
    {
        Start = nodes[0] as StartNode
            ?? throw new ArgumentException($"节点表的首个节点必须是开始节点，收到 {nodes[0].GetType().Name}。");
        Edges = edges;
        _roots = [.. nodes.Where(node => node.Path.Parent is null).Select(root => Finalize(root, edges))];
        Nodes = [.. _roots.SelectMany(Flattened)];
        ExecutableNodes = [.. Nodes.OfType<ExecutableNode>()];
        Containers = [.. Nodes.OfType<ContainerNode>()];

        CheckTree(nodes.Count);
    }

    /// <summary>全部节点：开始、执行节点与容器，按先根序。</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>流程的唯一来源节点。</summary>
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

    /// <summary>按路径取节点：从根级按段逐层下钻，中间段必须是容器，不存在时抛出异常。</summary>
    public GraphNode this[NodePath path] =>
        ChildrenOf(path.Parent).FirstOrDefault(node => node.Label == path.Label)
        ?? throw new ArgumentException($"流程里没有节点 {path}。");

    /// <summary>直接子节点：容器给成员执行节点与子容器，路径为空给根级节点，按先根序。</summary>
    public IReadOnlyList<GraphNode> ChildrenOf(NodePath? container)
    {
        if (container is not { } parent)
        {
            return _roots;
        }

        return this[parent] is ContainerNode node ? node.Children : [];
    }

    /// <summary>节点直接所属的容器，根级节点为空。</summary>
    public ContainerNode? ContainerOf(NodePath path) =>
        path.Parent is { } parent ? this[parent] as ContainerNode : null;

    /// <summary>目标的入边，按编译顺序。边只汇入执行节点。</summary>
    public IReadOnlyList<FlowEdge> Incoming(NodePath target) =>
        this[target] is ExecutableNode executable ? executable.Incoming : [];

    /// <summary>来源的出边，按编译顺序。</summary>
    public IReadOnlyList<FlowEdge> Outgoing(NodePath source) => this[source].Outgoing;

    /// <summary>节点的输入侧方案，开始与不需来源即可挂起的类型为空。</summary>
    public NodeInputPlan? PlanOf(NodePath path) => this[path] switch
    {
        ExecutableNode executable => executable.Plan,
        ContainerNode container => container.Plan,
        _ => null,
    };

    /// <summary>节点类型的形状，开始为空。</summary>
    public NodeTypeShape? ShapeOf(NodePath path) => this[path] switch
    {
        ExecutableNode executable => executable.Type.Shape,
        ContainerNode container => container.Type.Shape,
        _ => null,
    };

    /// <summary>来源节点某端口声明的类型，容器与未声明按 Text。供边规则按类型校验。</summary>
    public PortType TypeOf(NodePath node, PortName port) =>
        this[node] switch
        {
            ExecutableNode executable => executable.FindPort(port)?.Type ?? PortType.Text,
            StartNode start => start.FindPort(port)?.Type ?? PortType.Text,
            _ => PortType.Text,
        };

    /// <summary>把与节点相关的边挂到节点上，子节点递归重建，节点记录构造后不再变化。
    /// 容器与执行节点带上自己的入出边，输入侧方案由节点按入边算出。</summary>
    private static GraphNode Finalize(GraphNode node, IReadOnlyList<FlowEdge> edges) => node switch
    {
        StartNode start => start with { Outgoing = [.. edges.Where(edge => edge.Source == start.Path)] },
        ContainerNode container => container with
        {
            Children = [.. container.Children.Select(child => Finalize(child, edges))],
            Outgoing = [.. edges.Where(edge => edge.Source == container.Path)],
        },
        ExecutableNode executable => executable with
        {
            Incoming = [.. edges.Where(edge => edge.Target == executable.Path)],
            Outgoing = [.. edges.Where(edge => edge.Source == executable.Path)],
        },
        _ => node,
    };

    /// <summary>节点与它已重建的子树，先根序。</summary>
    private static IEnumerable<GraphNode> Flattened(GraphNode node) =>
        node is ContainerNode container
            ? [node, .. container.Children.SelectMany(Flattened)]
            : [node];

    /// <summary>树的形状检查：同层节点标签唯一、子节点路径与父一致，且节点表里的节点在树上各出现一次，边两端都能下钻到。</summary>
    private void CheckTree(int supplied)
    {
        CheckLevel(_roots, parent: null);
        foreach (ContainerNode container in Containers)
        {
            CheckLevel(container.Children, container.Path);
        }

        if (TreeCount(_roots) != supplied)
        {
            throw new ArgumentException("节点表里的节点数与流程树上的节点数不一致，同一条路径上可能出现多个节点。");
        }

        foreach (FlowEdge edge in Edges)
        {
            _ = this[edge.Source];
            _ = this[edge.Target];
        }
    }

    /// <summary>从一层子节点沿子节点表数已挂到树上的节点数。</summary>
    private static int TreeCount(IReadOnlyList<GraphNode> level) =>
        level.Sum(child => 1 + (child is ContainerNode container ? TreeCount(container.Children) : 0));

    /// <summary>一层子节点：同层节点标签唯一，子节点路径必须是父路径加本段。</summary>
    private static void CheckLevel(IReadOnlyList<GraphNode> children, NodePath? parent)
    {
        string where = parent is { } container ? $"容器 {container}" : "根级";
        HashSet<NodeLabel> seen = [];
        foreach (GraphNode child in children)
        {
            if (!seen.Add(child.Label))
            {
                throw new ArgumentException($"{where}的节点标签 {child.Label} 重复，节点标签只需同一容器内唯一。");
            }

            if (child.Path.Parent != parent)
            {
                throw new ArgumentException($"{where}的子节点 {child.Path} 不在这一层。");
            }
        }
    }

    /// <summary>真实依赖环的强连通分量：来源容器展开到全部成员后建邻接，只取成员多于一个或带自连环的分量，成员按分量给出。用于环合法性校验。</summary>
    public IReadOnlyList<NodePath[]> Loops()
    {
        ExecutableNode[] executables = [.. ExecutableNodes];
        var rank = new Dictionary<NodePath, int>();
        for (int i = 0; i < executables.Length; i++)
        {
            rank[executables[i].Path] = i;
        }

        var adjacent = new List<List<int>>(executables.Length);
        for (int i = 0; i < executables.Length; i++)
        {
            adjacent.Add([]);
        }

        foreach (FlowEdge edge in Edges)
        {
            foreach (NodePath source in SourcesIn(edge.Source))
            {
                adjacent[rank[source]].Add(rank[edge.Target]);
            }
        }

        var loops = new List<NodePath[]>();
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
                loops.Add([.. members.Select(member => executables[member].Path)]);
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

    /// <summary>容器及它的全部子容器里的执行节点路径，递归展开。容器出边的激活消息沿成员到目标的路由边投递。</summary>
    public IReadOnlyList<NodePath> ExecutablesIn(NodePath source) => [.. SourcesIn(source).Distinct()];

    /// <summary>来源节点展开出的执行节点路径集合：执行节点给自身，容器递归给全部成员，开始没有执行节点。</summary>
    private IEnumerable<NodePath> SourcesIn(NodePath source) =>
        this[source] switch
        {
            ExecutableNode _ => [source],
            ContainerNode container => container.Children.SelectMany(child => SourcesIn(child.Path)),
            _ => [],
        };
}
