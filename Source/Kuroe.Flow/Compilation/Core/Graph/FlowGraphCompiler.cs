using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;
namespace Kuroe.Flow.Compilation.Graph;

/// <summary>把流程树编译成执行视图：流程开始端口是独立来源，节点以从根到本节点的路径标识，容器登记直接子节点，
/// From 启动组编译为带取值域与输入类型的绑定：输入类型非 Text 的按整份消费，其余按来源与目标模式推导。
/// 引用按作用域链解析成路径：从引用者所在容器逐层向外，先看该层的直接子节点，再看该层子树，定义内引用因此可以指向子树的成员。
/// 要求已通过 FlowRules 校验，输入绑定引用与节点标签都可解析。传入的树必须是展开后的节点树。</summary>
internal sealed class FlowGraphCompiler
{
    /// <summary>报错作用域，引用解析与结构问题按它给出流程级错误。</summary>
    private readonly NodeScope _scope;

    /// <summary>流程开始端口，开始端口不是节点，它的声明来自流程。</summary>
    private readonly IReadOnlyList<Port> _startPorts;

    /// <summary>编译中收集的错误，引用无法解析时不抛出，交调用方按普通校验错误处理。</summary>
    private readonly List<Error> _errors = [];

    /// <summary>根级节点，流程开始端口不进入节点表。</summary>
    private readonly List<Pending> _roots = [];

    /// <summary>编译出的驱动绑定，按登记顺序。</summary>
    private readonly List<InputBinding> _bindings = [];

    private FlowGraphCompiler(
        IReadOnlyList<Port> startPorts,
        NodeScope scope)
    {
        _startPorts = startPorts;
        _scope = scope;
    }

    /// <summary>编译流程树：把流程树展开成执行视图。引用无法按作用域解析与绑定端口引用等结构问题记入错误结果，其余情况给出执行视图。</summary>
    public static ErrorOr<NodeGraph> Compile(FlowDefinition flow) =>
        Compile(flow, NodeScope.Flow(flow.Name.Value));

    private static ErrorOr<NodeGraph> Compile(FlowDefinition flow, NodeScope scope)
    {
        var compiler = new FlowGraphCompiler(flow.StartPorts, scope);
        compiler._roots.Add(Build(flow.RootNode, parent: null));

        return compiler.Assemble();
    }

    /// <summary>编译期的节点：定义、路径、父与直接子节点，以及本轮编译落定的端口绑定。</summary>
    private sealed class Pending(NodeSpec entry, NodePath path, Pending? parent)
    {
        public NodeSpec Entry { get; } = entry;
        public NodePath Path { get; } = path;
        public Pending? Parent { get; } = parent;
        public List<Pending> Children { get; } = [];
        public Dictionary<PortName, OutputBinding> Bindings { get; } = [];
        public Dictionary<PortName, InputBinding> InputBindings { get; } = [];
    }

    /// <summary>按先根序建出编译期的节点树：路径是父路径加本节点标签，层级由树给出，不靠标签前缀。</summary>
    private static Pending Build(NodeSpec spec, Pending? parent)
    {
        var pending = new Pending(spec, parent is null ? new NodePath([spec.Label]) : parent.Path.Append(spec.Label), parent);
        parent?.Children.Add(pending);

        foreach (NodeSpec child in spec.Nodes ?? [])
        {
            Build(child, pending);
        }

        return pending;
    }

    /// <summary>解析引用、把绑定与方案挂到节点上，按先根序给出节点表，构造执行视图。解析失败时只给错误。</summary>
    private ErrorOr<NodeGraph> Assemble()
    {
        LinkWiring();

        if (_errors.Count > 0)
        {
            return _errors;
        }

        List<GraphNode> nodes = [.. _roots.Select(NewNode).SelectMany(Flattened)];

        return new NodeGraph(_startPorts, nodes, _bindings);
    }

    /// <summary>节点与它的后代，按先根序。</summary>
    private static IEnumerable<GraphNode> Flattened(GraphNode node) =>
        node is ContainerNode container
            ? [node, .. container.Children.SelectMany(Flattened)]
            : [node];

    /// <summary>按条目构造节点：有子节点的是容器，有执行配置的是执行节点，两者都缺时是展开后不应出现的结构。</summary>
    private static GraphNode NewNode(Pending pending) => pending.Entry switch
    {
        { Nodes.Count: > 0 } => NewContainer(pending),
        { Spec: { } spec } => NewExecutable(pending, spec),
        _ => throw new InvalidOperationException($"节点 {pending.Path} 既没有子节点也没有执行配置。"),
    };

    /// <summary>把 From 启动组、输入绑定与输出端口来源解析成路径，落定全部接线。</summary>
    private void LinkWiring()
    {
        foreach (Pending pending in Flatten(_roots))
        {
            if (pending.Entry.Spec is not null)
            {
                LinkExecutable(pending);
            }
            else if (pending.Entry.IsContainer)
            {
                LinkContainerFrom(pending);
            }

            LinkBindings(pending);
            LinkOutputs(pending);
        }
    }

    /// <summary>执行节点的 From 启动组编译成绑定：组序号按组序，来源与端口按作用域解析，输入类型取自目标输入端口声明。</summary>
    private void LinkExecutable(Pending pending)
    {
        int groupIndex = 0;
        foreach (FromGroup group in pending.Entry.From)
        {
            foreach (FromLink link in group.Links)
            {
                if (!TryResolveFrom(pending, link, container: false, out BindingSource source, out InputPort input))
                {
                    continue;
                }

                AddBinding(
                    source,
                    pending.Path,
                    link.Input,
                    input.Type,
                    input.Trigger,
                    link.Condition,
                    groupIndex);
            }

            groupIndex++;
        }
    }

    /// <summary>容器的 From 组编译成传入端口绑定：组内目标必须是动态输入，触发端口只门控发布，其余端口作为成员取值来源。
    /// 绑定挂在容器自身，不进入驱动绑定表，来源版本不直接驱动发布。</summary>
    private void LinkContainerFrom(Pending pending)
    {
        foreach (FromLink link in pending.Entry.From.SelectMany(group => group.Links))
        {
            if (!TryResolveFrom(pending, link, container: true, out BindingSource source, out InputPort input))
            {
                continue;
            }

            pending.InputBindings[link.Input] = new InputBinding(
                pending.Path,
                source,
                link.Input,
                input.Type,
                input.Trigger,
                input.Trigger ? BindingRole.Gate : BindingRole.Alias,
                Condition: link.Condition);
        }
    }

    /// <summary>解析一条 From 连接：来源按作用域链解析，目标输入取自生效输入且必须是动态输入。
    /// 容器的连接还要求来源在容器外部。</summary>
    private bool TryResolveFrom(
        Pending pending,
        FromLink link,
        bool container,
        out BindingSource source,
        out InputPort input)
    {
        source = default;
        input = default;

        if (SourceOf(link.Ref, pending, "From") is not { } resolved)
        {
            return false;
        }

        if (container && resolved.Node is { } inside && IsInside(pending, inside))
        {
            _errors.Add(_scope.Node(pending.Path.Value,
                $"From 连接 {link.Ref.Display} 的来源 {resolved.Display} 必须在容器外部。"));
            return false;
        }

        if (PortRules.Find(pending.Entry.EffectiveInputs, link.Input) is not { } declared)
        {
            _errors.Add(_scope.Node(pending.Path.Value,
                $"From 连接的目标输入端口 {link.Input} 没有在节点上声明。"));
            return false;
        }

        if (declared.IsStatic)
        {
            _errors.Add(_scope.Node(pending.Path.Value,
                $"From 不能连接静态输入端口 {link.Input}，静态输入用 Bindings 绑定。"));
            return false;
        }

        source = resolved;
        input = declared;
        return true;
    }

    /// <summary>来源是否落在节点自身或它的子树里。容器接线与容器静态绑定都要求来源在外部。</summary>
    private static bool IsInside(Pending pending, NodePath source) =>
        source == pending.Path || source.IsUnder(pending.Path);

    /// <summary>解析一条端口引用：开始端口直接给流程来源，其余按作用域链解析节点。解析失败时给空来源。</summary>
    private BindingSource? SourceOf(PortRef reference, Pending referrer, string field)
    {
        if (reference.IsStart)
        {
            return _startPorts.Find(reference.Port) is null
                ? MissingStartPort(referrer, reference.Port, field)
                : BindingSource.ForStart(reference.Port);
        }

        if (reference.Source is not { } label)
        {
            _errors.Add(_scope.Node(referrer.Path.Value, $"{field} 引用 {reference.Display} 没有绑定来源。"));
            return null;
        }

        if (Resolve(label, referrer, field) is not { } source)
        {
            return null;
        }

        return BindingSource.ForNode(source.Path, reference.Port);
    }

    /// <summary>报告流程端口没有声明，返回空来源。</summary>
    private BindingSource? MissingStartPort(Pending referrer, PortName port, string field)
    {
        _errors.Add(_scope.Node(referrer.Path.Value, $"{field} 引用的流程开始端口 {port} 不在流程里。"));
        return null;
    }

    /// <summary>解析静态输入绑定的来源路径。@端口 应由展开层解析成具体来源，容器的绑定来源必须在容器外部。</summary>
    private void LinkBindings(Pending pending)
    {
        if (pending.Entry.Bindings is not { Count: > 0 } bindings)
        {
            return;
        }

        bool container = pending.Entry.IsContainer;
        foreach ((PortName target, PortRef reference) in bindings)
        {
            if (SourceOf(reference, pending, "Bindings") is not { } source)
            {
                continue;
            }

            if (container && source.Node is { } inside && IsInside(pending, inside))
            {
                _errors.Add(_scope.Node(pending.Path.Value,
                    $"输入绑定 {reference.Display} 的来源 {source.Display} 必须在容器外部。"));
                continue;
            }

            InputPort? input = pending.Entry.EffectiveInputs.Find(target);
            pending.InputBindings[target] = new InputBinding(
                pending.Path,
                source,
                target,
                input?.Type,
                input?.Trigger ?? false,
                BindingRole.Static);
        }
    }

    /// <summary>输出端口的来源解析成内部端口路径：@端口 指向本节点输入，成员@端口 指向直接子节点输出。</summary>
    private void LinkOutputs(Pending pending)
    {
        foreach (Port output in pending.Entry.Outputs)
        {
            if (output.Ref is not { } reference)
            {
                continue;
            }

            if (reference.Source is not { } label)
            {
                pending.Bindings[output.Name] = new OutputBinding(pending.Path, reference.Port);
                continue;
            }

            if (ResolveIn(pending.Children, label) is not { } member)
            {
                _errors.Add(_scope.Node(pending.Path.Value,
                    $"输出端口 {output.Name} 的目标 {reference.Display} 不在节点 {pending.Path} 的子树里。"));
                continue;
            }

            pending.Bindings[output.Name] = new OutputBinding(member.Path, reference.Port);
        }
    }

    /// <summary>登记一条驱动绑定：绑定表按登记顺序，入出绑定由执行视图在构造时挂到节点上。</summary>
    private void AddBinding(
        BindingSource source,
        NodePath target,
        PortName targetInput,
        PortType type,
        bool trigger,
        TriggerCondition condition,
        int group) =>
        _bindings.Add(new InputBinding(
            target, source, targetInput, type, trigger, BindingRole.Dynamic, group, condition));

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

    /// <summary>执行节点：类型与执行配置落定。</summary>
    private static ExecutableNode NewExecutable(Pending pending, ExecutableSpec spec)
    {
        NodeSpec entry = pending.Entry;

        return new ExecutableNode
        {
            Path = pending.Path,
            Type = TypeOf(entry),
            Spec = spec,
            Inputs = entry.EffectiveInputs,
            Outputs = entry.Outputs,
            InputBindings = pending.InputBindings.Count > 0 ? pending.InputBindings : null,
            OutputBindings = pending.Bindings.Count > 0 ? pending.Bindings : null,
            MaxExecutions = spec.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
        };
    }

    /// <summary>容器节点：类型、端口与直接子节点落定，输出端口绑定一并挂上。</summary>
    private static ContainerNode NewContainer(Pending pending)
    {
        List<GraphNode> children = [.. pending.Children.Select(NewNode)];

        return new ContainerNode
        {
            Path = pending.Path,
            Type = TypeOf(pending.Entry),
            Children = children,
            Inputs = pending.Entry.EffectiveInputs,
            Outputs = pending.Entry.Outputs,
            OutputBindings = pending.Bindings.Count > 0 ? pending.Bindings : null,
            InputBindings = pending.InputBindings.Count > 0 ? pending.InputBindings : null,
            MaxExecutions = pending.Entry.MaxExecutions ?? int.MaxValue,
        };
    }

    /// <summary>节点的类型描述。编译只接受已通过校验的节点树，类型缺失在此防御性报出。</summary>
    private static INodeType TypeOf(NodeSpec node) =>
        node.Type ?? throw new InvalidOperationException($"节点 {node.Label} 没有类型，编译只接受已校验的节点树。");
}
