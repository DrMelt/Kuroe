using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
namespace Kuroe.Flow.Compiler.Compilation;

/// <summary>把流程树编译成执行视图：开始是唯一来源，节点以从根到本节点的路径标识，容器登记直接子节点，
/// From 启动组编译为带取值域与输入类型的边：输入类型非 Text 的按整份消费，其余按来源与目标模式推导。
/// 引用按作用域链解析成路径：从引用者所在容器逐层向外，先看该层的直接子节点，再看该层子树，定义内引用因此可以指向子树的成员。
/// 要求已通过 FlowRules 校验，静态绑定与节点标签都可解析。传入的树必须是展开后的节点树。</summary>
internal sealed class FlowCompilation
{
    /// <summary>报错作用域，引用解析与结构问题按它给出流程级错误。</summary>
    private readonly NodeScope _scope;

    /// <summary>开始入口的输出端口，开始不在展开后的节点树里，它的端口声明来自流程。</summary>
    private readonly IReadOnlyList<Port> _startOutputs;

    /// <summary>开始端口到宿主输入名的绑定，静态开始端口的宿主来源按它判定。</summary>
    private readonly IReadOnlyDictionary<PortName, string> _hostStartInputs;

    /// <summary>编译中收集的错误，引用无法解析时不抛出，交调用方按普通校验错误处理。</summary>
    private readonly List<Error> _errors = [];

    /// <summary>根级节点，开始不在其中，它由编译直接构造。</summary>
    private readonly List<Pending> _roots = [];

    /// <summary>编译出的边，按登记顺序。</summary>
    private readonly List<FlowEdge> _edges = [];

    private FlowCompilation(
        IReadOnlyList<Port> startOutputs,
        IReadOnlyDictionary<PortName, string> hostStartInputs,
        NodeScope scope)
    {
        _startOutputs = startOutputs;
        _hostStartInputs = hostStartInputs;
        _scope = scope;
    }

    /// <summary>开始节点的路径，流程编译视图里唯一且固定。</summary>
    private static NodePath StartPath => new([StartNode.ReservedLabel]);

    /// <summary>编译流程树：把流程树展开成执行视图。引用无法按作用域解析与绑定端口引用等结构问题记入错误结果，其余情况给出执行视图。</summary>
    public static ErrorOr<NodeGraph> Compile(FlowDefinition flow) =>
        Compile(flow, NodeScope.Flow(flow.Name.Value));

    private static ErrorOr<NodeGraph> Compile(FlowDefinition flow, NodeScope scope)
    {
        var compiler = new FlowCompilation(flow.StartOutputs, flow.HostStartInputs, scope);
        compiler._roots.Add(Build(flow.RootNode, parent: null));

        return compiler.Assemble();
    }

    /// <summary>编译期的节点：定义、路径、父与直接子节点，以及本轮编译落定的端口绑定与编译出的节点记录。</summary>
    private sealed class Pending(NodeSpec entry, NodePath path, Pending? parent)
    {
        public NodeSpec Entry { get; } = entry;
        public NodePath Path { get; } = path;
        public Pending? Parent { get; } = parent;
        public List<Pending> Children { get; } = [];
        public Dictionary<PortName, NodePort> Bindings { get; } = [];
        public Dictionary<PortName, StaticRef> StaticRefs { get; } = [];
        public Dictionary<PortName, StaticValue> StaticValues { get; } = [];
        public HashSet<PortName> StaticOutputs { get; } = [];

        /// <summary>编译出的节点记录，子树编译完成后填入。</summary>
        public GraphNode? Node { get; set; }
    }

    /// <summary>一条静态绑定的来源：来源路径与来源端口。</summary>
    private readonly record struct StaticRef(NodePath Source, PortName Port);

    /// <summary>按先根序建出编译期的节点树：路径是父路径加本节点标签，层级由树给出，不靠标签前缀。</summary>
    private static Pending Build(NodeSpec spec, Pending? parent)
    {
        var pending = new Pending(spec, parent is null ? new NodePath([spec.Label]) : parent.Path.Append(spec.Label), parent);
        parent?.Children.Add(pending);

        if (spec.ContainerOutputs is { Count: > 0 } outputs)
        {
            foreach ((PortName port, ContainerOutput output) in outputs)
            {
                if (output.IsStatic)
                {
                    pending.StaticOutputs.Add(port);
                }
            }
        }

        foreach (NodeSpec child in spec.Nodes ?? [])
        {
            Build(child, pending);
        }

        return pending;
    }

    /// <summary>解析引用、把边与方案挂到节点上，按先根序给出节点表，构造执行视图。解析失败时只给错误。</summary>
    private ErrorOr<NodeGraph> Assemble()
    {
        LinkEdges();
        FoldStaticValues();

        List<Pending> order = [.. Flatten(_roots)];
        foreach (Pending pending in Enumerable.Reverse(order))
        {
            pending.Node = pending.Entry.Nodes is { Count: > 0 } ? NewContainer(pending) : NewExecutable(pending);
        }

        return _errors.Count > 0
            ? _errors
            : new NodeGraph([NewStart(), .. order.Select(pending => pending.Node!)], _edges);
    }

    /// <summary>把 From 启动组与容器输出端口绑定解析成路径，落定全部边。</summary>
    private void LinkEdges()
    {
        foreach (Pending pending in Flatten(_roots))
        {
            if (pending.Entry.Spec is not null)
            {
                LinkExecutable(pending);
                LinkStatic(pending);
                continue;
            }

            LinkContainerOutputs(pending);
        }
    }

    /// <summary>执行节点的 From 启动组编译成边：组序号按组序，来源与端口按作用域解析，输入类型取自目标输入端口声明。</summary>
    private void LinkExecutable(Pending pending)
    {
        int groupIndex = 0;
        foreach (FromGroup group in pending.Entry.From)
        {
            foreach (FromLink link in group.Links)
            {
                if (link.Ref.Source is not { } label)
                {
                    _errors.Add(_scope.Node(pending.Path.Value,
                        $"From 连接 {link.Ref.Display} 是对绑定端口的引用，请写明来源节点。"));
                    continue;
                }

                if (SourceOf(label, pending, "From") is not { } from)
                {
                    continue;
                }

                PortType type = Ports.Find(pending.Entry.Inputs, link.Input)?.Type ?? PortTypes.Text;

                AddEdge(from, pending.Path, link.Ref.Port, link.Input, type, groupIndex);
            }

            groupIndex++;
        }
    }

    /// <summary>解析一条 From 连接的来源：开始是流程级来源，其余按作用域链解析。解析失败时给空来源。</summary>
    private NodePath? SourceOf(NodeLabel label, Pending referrer, string field) =>
        label == StartNode.ReservedLabel ? StartPath : Resolve(label, referrer, field)?.Path;

    /// <summary>解析静态绑定的来源路径。@端口 应由展开层解析成具体来源。</summary>
    private void LinkStatic(Pending pending)
    {
        if (pending.Entry.Static is not { Count: > 0 } bindings)
        {
            return;
        }

        foreach ((PortName target, PortRef reference) in bindings)
        {
            if (reference.Source is not { } label)
            {
                _errors.Add(_scope.Node(pending.Path.Value,
                    $"静态绑定 {reference.Display} 没有解析到来源节点。"));
                continue;
            }

            if (SourceOf(label, pending, "Static") is { } source)
            {
                pending.StaticRefs[target] = new StaticRef(source, reference.Port);
            }
        }
    }

    /// <summary>容器输出端口的名字引用解析成成员路径，端口名按声明原样保留。解析失败时给出错误。</summary>
    private void LinkContainerOutputs(Pending pending)
    {
        if (pending.Entry.ContainerOutputs is not { Count: > 0 } outputs)
        {
            return;
        }

        foreach ((PortName port, ContainerOutput output) in outputs)
        {
            if (output.Ref.Source is not { } label)
            {
                _errors.Add(_scope.Node(pending.Path.Value,
                    $"容器输出端口 {port} 的绑定是对绑定端口的引用，绑定目标写成目标成员标签。"));
                continue;
            }

            if (ResolveIn(pending.Children, label) is not { } member)
            {
                _errors.Add(_scope.Node(pending.Path.Value,
                    $"容器输出端口 {port} 的绑定目标 {label} 不在容器 {pending.Path} 的子树里。"));
                continue;
            }

            pending.Bindings[port] = new NodePort(member.Path, output.Ref.Port);
        }
    }

    /// <summary>把所有静态输入折叠成值来源。来源必须是静态端口；常量在装载期给出，宿主开始端口在任务启动时解析。</summary>
    private void FoldStaticValues()
    {
        foreach (Pending pending in Flatten(_roots))
        {
            if (pending.Entry.Spec is null)
            {
                continue;
            }

            foreach (InputPort input in pending.Entry.EffectiveInputs.Where(port => port.IsStatic))
            {
                if (pending.StaticRefs.TryGetValue(input.Name, out StaticRef reference))
                {
                    if (ResolveStaticRef(reference, []) is { } value)
                    {
                        pending.StaticValues[input.Name] = value;
                    }

                    continue;
                }

                if (pending.Entry.Static?.ContainsKey(input.Name) == true)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(input.Default))
                {
                    pending.StaticValues[input.Name] = StaticValue.Constant(input.Default);
                    continue;
                }

                _errors.Add(_scope.Node(pending.Path.Value,
                    $"静态输入端口 {input.Name.Value} 没有绑定来源或默认值。"));
            }
        }
    }

    /// <summary>求一条静态绑定的值来源。开始端口给常量或宿主端口，容器静态输出沿绑定下钻，执行节点没有静态输出。</summary>
    private StaticValue? ResolveStaticRef(StaticRef reference, HashSet<StaticRef> visiting)
    {
        StaticRef key = reference;
        if (!visiting.Add(key))
        {
            _errors.Add(_scope.Node(reference.Source.Value, $"静态端口 {reference.Port} 形成环。"));
            return null;
        }

        try
        {
            if (reference.Source == StartPath)
            {
                Port? port = _startOutputs.Find(reference.Port);
                if (port is null)
                {
                    _errors.Add(_scope.Node(reference.Source.Value, $"开始没有输出端口 {reference.Port}。"));
                    return null;
                }

                Port declared = port.Value;
                if (!declared.IsStatic)
                {
                    _errors.Add(_scope.Node(reference.Source.Value,
                        $"开始端口 {reference.Port} 不是静态端口。"));
                    return null;
                }

                if (declared.Value is { Length: > 0 } value)
                {
                    return StaticValue.Constant(value);
                }

                if (_hostStartInputs.ContainsKey(reference.Port))
                {
                    return StaticValue.Host(reference.Port);
                }

                _errors.Add(_scope.Node(reference.Source.Value,
                    $"静态开始端口 {reference.Port} 没有 Value 或宿主输入绑定。"));
                return null;
            }

            Pending? source = FindByPath(reference.Source);
            if (source is null)
            {
                _errors.Add(_scope.Node(reference.Source.Value, $"静态来源 {reference.Source} 不在流程里。"));
                return null;
            }

            return source.Entry.Spec is not null
                ? MissingStaticOutput(reference)
                : ContainerStaticValue(source, reference.Port, visiting);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    /// <summary>求容器静态输出的值来源：输出必须绑定到子树的静态端口。</summary>
    private StaticValue? ContainerStaticValue(Pending container, PortName port, HashSet<StaticRef> visiting)
    {
        if (!container.StaticOutputs.Contains(port))
        {
            _errors.Add(_scope.Node(container.Path.Value,
                $"容器 {container.Path} 没有静态输出端口 {port}。"));
            return null;
        }

        if (!container.Bindings.TryGetValue(port, out NodePort target))
        {
            _errors.Add(_scope.Node(container.Path.Value,
                $"容器静态输出端口 {port} 没有绑定目标。"));
            return null;
        }

        return StaticPortValue(FindByPath(target.Node), target.Port, visiting);
    }

    /// <summary>求子树静态端口的值来源：执行节点取静态输入，容器取静态输出。</summary>
    private StaticValue? StaticPortValue(Pending? node, PortName port, HashSet<StaticRef> visiting)
    {
        if (node is null)
        {
            _errors.Add(_scope.Node(port.Value, $"静态端口 {port} 的目标不在流程里。"));
            return null;
        }

        StaticRef key = new(node.Path, port);
        if (!visiting.Add(key))
        {
            _errors.Add(_scope.Node(node.Path.Value, $"静态端口 {port} 形成环。"));
            return null;
        }

        try
        {
            if (node.Entry.Spec is not null)
            {
                InputPort? input = node.Entry.EffectiveInputs.Find(port);
                if (input is not { } declared || !declared.IsStatic)
                {
                    _errors.Add(_scope.Node(node.Path.Value,
                        $"静态输出端口的目标 {port} 不是静态输入。"));
                    return null;
                }

                if (node.StaticRefs.TryGetValue(port, out StaticRef reference))
                {
                    return ResolveStaticRef(reference, visiting);
                }

                if (!string.IsNullOrWhiteSpace(declared.Default))
                {
                    return StaticValue.Constant(declared.Default);
                }

                _errors.Add(_scope.Node(node.Path.Value,
                    $"静态输入端口 {port} 没有绑定来源或默认值。"));
                return null;
            }

            return ContainerStaticValue(node, port, visiting);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    /// <summary>执行节点没有静态输出端口。</summary>
    private StaticValue? MissingStaticOutput(StaticRef reference)
    {
        _errors.Add(_scope.Node(reference.Source.Value,
            $"执行节点 {reference.Source} 的输出端口 {reference.Port} 不是静态端口。"));
        return null;
    }

    /// <summary>按路径在编译期节点树里定位节点。</summary>
    private Pending? FindByPath(NodePath path)
    {
        IReadOnlyList<Pending> level = _roots;
        Pending? current = null;
        foreach (NodeLabel label in path.Levels)
        {
            current = level.FirstOrDefault(node => node.Entry.Label == label);
            if (current is null)
            {
                return null;
            }

            level = current.Children;
        }

        return current;
    }

    /// <summary>登记一条边：边表按登记顺序，入出边由执行视图在构造时挂到节点上。</summary>
    private void AddEdge(
        NodePath source,
        NodePath target,
        PortName sourcePort,
        PortName targetInput,
        PortType type,
        int group) =>
        _edges.Add(new FlowEdge(source, target, sourcePort, targetInput, type, group));

    /// <summary>按标签在作用域子树里解析节点：从引用者所在容器逐层向外，每层先看直接子节点再看子树。找不到时记错误给空。</summary>
    private Pending? Resolve(NodeLabel label, Pending referrer, string field)
    {
        Pending? scope = referrer.Parent;
        while (true)
        {
            if (ResolveIn(scope?.Children ?? _roots, label) is { } found)
            {
                return found;
            }

            if (scope is null)
            {
                _errors.Add(_scope.Node(referrer.Path.Value, $"{field} 引用的节点 {label} 不在流程里。"));
                return null;
            }

            scope = scope.Parent;
        }
    }

    /// <summary>按标签在一层子节点及其子树里解析节点：直接子节点优先，子树里命中多个或没有命中时给空。</summary>
    private Pending? ResolveIn(IReadOnlyList<Pending> children, NodeLabel label)
    {
        if (children.FirstOrDefault(child => child.Entry.Label == label) is { } direct)
        {
            return direct;
        }

        List<Pending> deeper = [.. children.SelectMany(Descendants).Where(node => node.Entry.Label == label)];
        if (deeper.Count > 1)
        {
            _errors.Add(_scope.Node(label.Value, $"引用 {label} 在作用域里命中多个节点，同层标签不能重复。"));
            return null;
        }

        return deeper.Count == 1 ? deeper[0] : null;
    }

    /// <summary>子树按先根序展开，容器先于它的子节点。</summary>
    private static IEnumerable<Pending> Flatten(IReadOnlyList<Pending> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    /// <summary>节点的全部后代，不含自身，按先根序。</summary>
    private static IEnumerable<Pending> Descendants(Pending node) => Flatten(node.Children);

    /// <summary>开始节点：不属于任何容器，不被任何容器包含，任务启动即已放行。</summary>
    private StartNode NewStart() => new()
    {
        Path = StartPath,
        Outputs = _startOutputs,
    };

    /// <summary>执行节点：类型与执行配置落定。</summary>
    private static ExecutableNode NewExecutable(Pending pending)
    {
        NodeSpec entry = pending.Entry;
        ExecutableSpec spec = entry.Spec!;

        return new ExecutableNode
        {
            Path = pending.Path,
            Type = TypeOf(entry),
            Spec = spec,
            Inputs = entry.EffectiveInputs,
            Outputs = entry.Outputs,
            StaticValues = pending.StaticValues.Count > 0 ? pending.StaticValues : null,
            MaxExecutions = spec.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
        };
    }

    /// <summary>容器节点：类型与直接子节点落定，命名输出端口绑定一并挂上。</summary>
    private static ContainerNode NewContainer(Pending pending)
    {
        List<GraphNode> children = [.. pending.Children.Select(child => child.Node!)];

        return new ContainerNode
        {
            Path = pending.Path,
            Type = TypeOf(pending.Entry),
            Children = children,
            PortBindings = pending.Bindings.Count > 0 ? pending.Bindings : null,
            StaticOutputs = pending.StaticOutputs.Count > 0 ? pending.StaticOutputs : null,
        };
    }

    /// <summary>节点的类型描述。编译只接受已通过校验的节点树，类型缺失在此防御性报出。</summary>
    private static INodeType TypeOf(NodeSpec node) =>
        node.Type ?? throw new InvalidOperationException($"节点 {node.Label} 没有类型，编译只接受已校验的节点树。");
}
