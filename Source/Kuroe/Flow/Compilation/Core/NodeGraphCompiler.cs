using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Compilation;

/// <summary>把语言语义映射成执行视图：节点以从根到本节点的路径标识，容器登记直接子节点。
/// 绑定来源、启动组、触发条件、输出映射与静态取值终点都取自 <see cref="FlowSemantics"/>，这里不再解析引用。</summary>
internal sealed class NodeGraphCompiler
{
    private readonly FlowSemantics _semantics;
    private readonly List<Pending> _roots = [];
    private readonly List<InputBinding> _bindings = [];
    private readonly Dictionary<NodePath, IReadOnlyDictionary<PortName, StaticResolution>> _staticInputs = [];
    private readonly Dictionary<NodePath, IReadOnlyDictionary<PortName, StaticResolution>> _constantOutputs = [];

    private NodeGraphCompiler(FlowSemantics semantics)
    {
        _semantics = semantics;
    }

    /// <summary>按语义结果编译流程树，建立执行视图。</summary>
    public static NodeGraph Compile(FlowSemantics semantics)
    {
        var compiler = new NodeGraphCompiler(semantics);
        compiler._roots.Add(Build(semantics.Definition.RootNode, parent: null));

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

    /// <summary>把绑定与方案挂到节点上，按先根序给出节点表，构造执行视图。</summary>
    private NodeGraph Assemble()
    {
        LinkWiring();

        List<GraphNode> nodes = [.. _roots.Select(NewNode).SelectMany(Flattened)];

        return new NodeGraph(
            _semantics.Definition.StartPorts,
            nodes,
            _bindings,
            _staticInputs,
            _constantOutputs);
    }

    /// <summary>把语义结果落定到每个节点：静态输入绑定、动态输入绑定、输出绑定与静态取值解析。</summary>
    private void LinkWiring()
    {
        foreach (Pending pending in Flatten(_roots))
        {
            if (_semantics.Find(pending.Path) is not { } node)
            {
                continue;
            }

            LinkStatic(pending, node);
            if (pending.Entry.Spec is not null)
            {
                LinkExecutable(pending, node);
            }
            else if (pending.Entry.IsContainer)
            {
                LinkContainer(pending, node);
            }

            LinkOutputs(pending, node);
            CollectResolutions(pending, node);
        }
    }
    /// <summary>静态输入绑定挂在节点自身，不进入驱动绑定表。</summary>
    private static void LinkStatic(Pending pending, FlowSemanticNode node)
    {
        foreach (FlowSemanticInput input in node.Inputs.Where(input => !input.IsLinked && input.Binding is not null))
        {
            FlowBinding binding = input.Binding!.Value;
            pending.InputBindings[input.Port.Name] = new InputBinding(
                pending.Path,
                ToBindingSource(binding),
                input.Port.Name,
                input.Port.Type,
                input.Port.Trigger,
                BindingRole.Static);
        }
    }

    /// <summary>执行节点的动态输入进入驱动绑定表，组序号与触发条件取自语义结果。</summary>
    private void LinkExecutable(Pending pending, FlowSemanticNode node)
    {
        foreach (FlowSemanticInput input in node.Inputs.Where(input => input.IsLinked && input.Binding is not null))
        {
            FlowBinding binding = input.Binding!.Value;
            _bindings.Add(new InputBinding(
                pending.Path,
                ToBindingSource(binding),
                input.Port.Name,
                input.Port.Type,
                input.Port.Trigger,
                BindingRole.Dynamic,
                input.Group,
                input.Condition));
        }
    }

    /// <summary>容器的动态输入挂在容器自身：触发端口是门控，其余端口是成员取值别名。</summary>
    private static void LinkContainer(Pending pending, FlowSemanticNode node)
    {
        foreach (FlowSemanticInput input in node.Inputs.Where(input => input.IsLinked && input.Binding is not null))
        {
            FlowBinding binding = input.Binding!.Value;
            pending.InputBindings[input.Port.Name] = new InputBinding(
                pending.Path,
                ToBindingSource(binding),
                input.Port.Name,
                input.Port.Type,
                input.Port.Trigger,
                input.Port.Trigger ? BindingRole.Gate : BindingRole.Alias,
                Condition: input.Condition);
        }
    }

    /// <summary>输出端口的直接来源落在输出绑定上，取值终点另行收集。</summary>
    private static void LinkOutputs(Pending pending, FlowSemanticNode node)
    {
        foreach (FlowSemanticOutput output in node.Outputs.Values)
        {
            if (output.Binding is not { Node: { } source } binding)
            {
                continue;
            }

            pending.Bindings[output.Port.Name] = new OutputBinding(source, binding.Port);
        }
    }

    /// <summary>收集静态输入与常量输出的取值终点，交给图视图在运行期取值。</summary>
    private void CollectResolutions(Pending pending, FlowSemanticNode node)
    {
        Dictionary<PortName, StaticResolution> inputs = [];
        foreach (FlowSemanticInput input in node.Inputs.Where(input => input.Port.IsStatic))
        {
            inputs[input.Port.Name] = ToResolution(pending.Path, input.Port.Name, input.Source);
        }

        if (inputs.Count > 0)
        {
            _staticInputs[pending.Path] = inputs;
        }

        Dictionary<PortName, StaticResolution> outputs = [];
        foreach (FlowSemanticOutput output in node.Outputs.Values.Where(output =>
            output.Port.IsConstant && output.Source is not null))
        {
            outputs[output.Port.Name] = ToResolution(pending.Path, output.Port.Name, output.Source!.Value);
        }

        if (outputs.Count > 0)
        {
            _constantOutputs[pending.Path] = outputs;
        }
    }

    private static BindingSource ToBindingSource(FlowBinding binding) =>
        binding.Node is { } node
            ? BindingSource.ForNode(node, binding.Port)
            : BindingSource.ForStart(binding.Port);

    private static StaticResolution ToResolution(NodePath owner, PortName port, FlowSource source) =>
        source.Kind switch
        {
            FlowSourceKind.StartFixed => StaticResolution.StartFixed(source.Port!.Value, source.Text!),
            FlowSourceKind.StartInput => StaticResolution.StartInjected(source.Port!.Value),
            FlowSourceKind.NodeConstant => StaticResolution.Literal(
                source.Node!.Value,
                source.Port!.Value,
                source.Text!),
            FlowSourceKind.RuntimeConstant => StaticResolution.RuntimeConstant(
                source.Node!.Value,
                source.Port!.Value),
            FlowSourceKind.Default => StaticResolution.Default(owner, port, source.Text!),
            _ => StaticResolution.Missing(owner, port, "静态来源无法解析。"),
        };

    /// <summary>子树按先根序展开，容器先于它的子节点。</summary>
    private static IEnumerable<Pending> Flatten(IReadOnlyList<Pending> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));
    /// <summary>节点与它的后代，按先根序。</summary>
    private static IEnumerable<GraphNode> Flattened(GraphNode node) =>
        node is ContainerNode container
            ? [node, .. container.Children.SelectMany(Flattened)]
            : [node];

    /// <summary>按条目构造节点：有子节点的是容器，有执行配置的是执行节点。</summary>
    private static GraphNode NewNode(Pending pending) => pending.Entry switch
    {
        { Nodes.Count: > 0 } => NewContainer(pending),
        { Spec: { } spec } => NewExecutable(pending, spec),
        _ => throw new InvalidOperationException($"节点 {pending.Path} 既没有子节点也没有执行配置。"),
    };

    /// <summary>执行节点：类型、执行配置与端口绑定落定。</summary>
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
            MaxExecutions = spec.MaxExecutions ?? ExecutableSpec.DefaultMaxExecutions,
        };
    }

    /// <summary>容器节点：类型、端口与直接子节点落定，端口绑定一并挂上。</summary>
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

    /// <summary>节点的类型描述。</summary>
    private static INodeType TypeOf(NodeSpec node) =>
        node.Type ?? throw new InvalidOperationException($"节点 {node.Label} 没有类型，编译只接受已校验的节点树。");
}