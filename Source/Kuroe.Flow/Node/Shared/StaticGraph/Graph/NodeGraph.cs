using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Node.StaticGraph.Graph;

/// <summary>流程编译出的执行视图：节点以从根到本节点的路径标识，执行节点与容器在同一棵树上。
/// 依赖由 From 绑定、静态输入指向的运行期常量依赖与容器触发输入共同表达，绑定的角色与取值域固化在绑定上。执行按“组齐备且有驱动则激活”推进。
/// 流程开始端口作为独立来源表保存在图上，不进入节点树。树与绑定在编译期落定，与节点相关的绑定在构造时挂到节点上。</summary>
public sealed class NodeGraph
{
    private readonly IReadOnlyList<GraphNode> _roots;
    private readonly Dictionary<NodePath, IReadOnlyDictionary<PortName, StaticResolution>> _staticInputs = [];
    private readonly Dictionary<NodePath, IReadOnlyDictionary<PortName, StaticResolution>> _constantOutputs = [];
    private readonly Dictionary<NodePath, IReadOnlyList<NodePath>> _dependents = [];
    private readonly Dictionary<NodePath, NodeInputPlan> _plans = [];

    /// <summary>构造执行视图：节点按路径落在同一棵树上，同层节点标签必须唯一，节点来源的绑定两端必须落在节点表里。
    /// 与节点相关的绑定在构造时挂到节点上：来源侧落在来源，驱动绑定落在执行节点。树检查通过后解析静态来源并生成输入侧方案。</summary>
    public NodeGraph(
        IReadOnlyList<Port> startPorts,
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<InputBinding> bindings)
    {
        if (nodes.Count == 0)
        {
            throw new ArgumentException("节点表不能为空，流程必须有一个根节点。");
        }

        StartPorts = startPorts;
        Bindings = [.. bindings];
        StartOutgoing = [.. Bindings.Where(binding => binding.Drives && binding.Source.IsStart)];
        _roots = [.. nodes.Where(node => node.Path.Parent is null).Select(root => Finalize(root, Bindings))];
        Nodes = [.. _roots.SelectMany(Flattened)];
        ExecutableNodes = [.. Nodes.OfType<ExecutableNode>()];
        Containers = [.. Nodes.OfType<ContainerNode>()];

        CheckTree(nodes.Count);
        StaticBindings = ResolveStaticBindings();
        BuildPlans();
        BuildDependents();
    }

    /// <summary>流程开始端口的声明表。</summary>
    public IReadOnlyList<Port> StartPorts { get; }

    /// <summary>全部节点：执行节点与容器，按先根序。</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>从流程开始端口出发的驱动绑定。</summary>
    public IReadOnlyList<InputBinding> StartOutgoing { get; }

    /// <summary>执行节点，按先根序。</summary>
    public IReadOnlyList<ExecutableNode> ExecutableNodes { get; }

    /// <summary>容器节点，按先根序。</summary>
    public IReadOnlyList<ContainerNode> Containers { get; }

    /// <summary>驱动绑定，按编译顺序。</summary>
    public IReadOnlyList<InputBinding> Bindings { get; }

    /// <summary>静态启动条件：静态输入绑定到 Once 来源节点唯一一次执行的产出。</summary>
    public IReadOnlyList<InputBinding> StaticBindings { get; }

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

    /// <summary>目标的驱动绑定，按编译顺序。绑定只汇入执行节点。</summary>
    public IReadOnlyList<InputBinding> Incoming(NodePath target) =>
        this[target] is ExecutableNode executable ? executable.Incoming : [];

    /// <summary>来源的驱动绑定，按编译顺序。</summary>
    public IReadOnlyList<InputBinding> Outgoing(NodePath source) => this[source].Outgoing;

    /// <summary>来源发布后需要重新评估的目标，包含静态依赖节点与使用该来源的容器触发输入。</summary>
    public IReadOnlyList<NodePath> Dependents(NodePath source) =>
        _dependents.GetValueOrDefault(source) ?? [];

    /// <summary>执行节点某静态输入的结构解析结果，未声明或未解析时为空。</summary>
    public StaticResolution? StaticInputResolution(NodePath node, PortName port) =>
        _staticInputs.TryGetValue(node, out IReadOnlyDictionary<PortName, StaticResolution>? inputs)
        && inputs.TryGetValue(port, out StaticResolution resolution)
            ? resolution
            : null;

    /// <summary>节点某常量输出的结构解析结果，未声明或未解析时为空。</summary>
    public StaticResolution? ConstantResolution(NodePath node, PortName port) =>
        _constantOutputs.TryGetValue(node, out IReadOnlyDictionary<PortName, StaticResolution>? outputs)
        && outputs.TryGetValue(port, out StaticResolution resolution)
            ? resolution
            : null;

    /// <summary>节点的输入侧方案，没有方案时为空。</summary>
    public NodeInputPlan? PlanOf(NodePath path) => _plans.GetValueOrDefault(path);

    /// <summary>节点类型的形状，没有类型时为空。</summary>
    public NodeTypeShape? ShapeOf(NodePath path) => this[path] switch
    {
        ExecutableNode executable => executable.Type.Shape,
        ContainerNode container => container.Type.Shape,
        _ => null,
    };

    /// <summary>来源节点某输出端口声明的类型，未声明按 Text。供绑定规则按类型校验。</summary>
    public PortType TypeOf(NodePath node, PortName port) =>
        this[node].FindPort(port)?.Type ?? PortTypes.Text;

    /// <summary>把与节点相关的绑定挂到节点上，子节点递归重建，节点记录构造后不再变化。
    /// 容器与执行节点带上自己的出入绑定，输入侧方案由执行视图在静态解析后生成。</summary>
    private static GraphNode Finalize(GraphNode node, IReadOnlyList<InputBinding> bindings) => node switch
    {
        ContainerNode container => container with
        {
            Children = [.. container.Children.Select(child => Finalize(child, bindings))],
            Outgoing = [.. bindings.Where(binding => binding.Drives && binding.Source.Node == container.Path)],
        },
        ExecutableNode executable => executable with
        {
            Incoming = [.. bindings.Where(binding => binding.Drives && binding.Target == executable.Path)],
            Outgoing = [.. bindings.Where(binding => binding.Drives && binding.Source.Node == executable.Path)],
        },
        _ => node,
    };

    /// <summary>按静态绑定链找出运行期常量条件，并登记每个静态输入与常量输出的结构解析结果。</summary>
    private IReadOnlyList<InputBinding> ResolveStaticBindings()
    {
        List<InputBinding> staticBindings = [];
        foreach (ExecutableNode executable in ExecutableNodes)
        {
            Dictionary<PortName, StaticResolution> inputs = executable.Inputs
                .Where(port => port.IsStatic)
                .Select(input => (input.Name, Resolution: StaticSources.ResolveInput(this, executable.Path, input.Name)))
                .ToDictionary(entry => entry.Name, entry => entry.Resolution);
            staticBindings.AddRange(inputs
                .Where(pair => pair.Value.Kind == StaticSourceKind.RuntimeConstant)
                .Select(pair => new InputBinding(
                    executable.Path,
                    pair.Value.Source,
                    pair.Key,
                    executable.FindInput(pair.Key)?.Type,
                    Role: BindingRole.Static)));

            if (inputs.Count > 0)
            {
                _staticInputs[executable.Path] = inputs;
            }
        }

        foreach (GraphNode node in Nodes)
        {
            Dictionary<PortName, StaticResolution> constants = node.Outputs
                .Where(port => port.IsConstant)
                .ToDictionary(port => port.Name, port => StaticSources.ResolveOutput(this, node.Path, port.Name));

            if (constants.Count > 0)
            {
                _constantOutputs[node.Path] = constants;
            }
        }

        return [.. staticBindings.Distinct()];
    }

    /// <summary>按来源建立重新评估目标的反查：静态启动条件在前，容器门控输入在后。</summary>
    private void BuildDependents()
    {
        Dictionary<NodePath, List<NodePath>> dependents = [];
        foreach (InputBinding binding in StaticBindings)
        {
            if (binding.Source.Node is { } source)
            {
                Add(source, binding.Target);
            }
        }

        foreach (ContainerNode container in Containers)
        {
            foreach (InputBinding binding in container.InputBindings?.Values.Where(binding => binding.Role == BindingRole.Gate) ?? [])
            {
                if (binding.Source.Node is { } source)
                {
                    Add(source, container.Path);
                }
            }
        }

        foreach ((NodePath source, List<NodePath> targets) in dependents)
        {
            _dependents[source] = [.. targets];
        }

        void Add(NodePath source, NodePath target)
        {
            if (!dependents.TryGetValue(source, out List<NodePath>? list))
            {
                list = [];
                dependents[source] = list;
            }

            if (!list.Contains(target))
            {
                list.Add(target);
            }
        }
    }

    /// <summary>生成执行节点与容器的输入侧方案，静态启动条件与产出模式一并落定。</summary>
    private void BuildPlans()
    {
        foreach (ExecutableNode executable in ExecutableNodes)
        {
            if (executable.Type.Shape.StartsUnaided)
            {
                _plans[executable.Path] = NodeInputPlan.External();
                continue;
            }

            IReadOnlyList<InputBinding> staticBindings =
                [.. StaticBindings.Where(binding => binding.Target == executable.Path)];
            _plans[executable.Path] =
                NodeInputPlan.Of(executable.Spec, executable.Incoming, executable.Inputs, staticBindings);
        }

        foreach (ContainerNode container in Containers)
        {
            _plans[container.Path] = NodeInputPlan.Of(container);
        }
    }

    /// <summary>节点与它已重建的子树，先根序。</summary>
    private static IEnumerable<GraphNode> Flattened(GraphNode node) =>
        node is ContainerNode container
            ? [node, .. container.Children.SelectMany(Flattened)]
            : [node];

    /// <summary>树的形状检查：同层节点标签唯一、子节点路径与父一致，且节点表里的节点在树上各出现一次，绑定两端都能下钻到。</summary>
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

        foreach (InputBinding binding in Bindings)
        {
            if (binding.Source.Node is { } source)
            {
                _ = this[source];
            }

            _ = this[binding.Target];
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

    /// <summary>容器及它的全部子容器里的执行节点路径，递归展开。容器下游的激活消息沿成员到目标的路由投递。</summary>
    public IReadOnlyList<NodePath> ExecutablesIn(NodePath source) => [.. SourcesIn(source).Distinct()];

    /// <summary>来源节点展开出的执行节点路径集合：执行节点给自身，容器递归给全部成员。</summary>
    private IEnumerable<NodePath> SourcesIn(NodePath source) =>
        this[source] switch
        {
            ExecutableNode _ => [source],
            ContainerNode container => container.Children.SelectMany(child => SourcesIn(child.Path)),
            _ => [],
        };
}
