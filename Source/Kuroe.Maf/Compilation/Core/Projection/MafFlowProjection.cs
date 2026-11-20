using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>单条流程的 MAF 投影：从展开后的流程定义建立步骤、连接与装配诊断。</summary>
internal static class MafFlowProjection
{
    private const char Segment = '·';

    /// <summary>把展开后的流程定义投影为 MAF 流程定义。</summary>
    internal static ErrorOr<MafFlowDefinition> Project(FlowDefinition flow, IMafNodeExecutions executions)
    {
        List<Error> errors = [];
        Tree tree = Tree.Build(flow.RootNode);
        List<Entry> executables =
            [.. tree.Entries.Where(entry => entry.Spec.Spec is not null && !entry.Spec.IsContainer)];

        if (executables.Count == 0)
        {
            return [FlowErrors.Body(flow.Name.Value, "流程没有执行节点，无法装配成 MAF 步骤。")];
        }

        Dictionary<NodePath, MafStepId> ids = StepIds(executables, flow.Name, errors);
        Dictionary<NodePath, MafStepPlan> plans = Plans(tree, flow, executables, ids, errors);
        IReadOnlyList<MafStartPort> startPorts = StartPorts(flow, errors);

        List<MafStep> steps = [];
        List<MafEdge> edges = [];
        foreach (Entry executable in executables)
        {
            if (!ids.TryGetValue(executable.Path, out MafStepId id)
                || !plans.TryGetValue(executable.Path, out MafStepPlan? plan)
                || executable.Spec.Type is not { } type)
            {
                continue;
            }

            IExecutionBinder? binder = executions.Execution(type);
            if (binder is null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"节点 {executable.Path} 的类型 {type.Kind.Value} 没有执行器绑定。"));
                continue;
            }

            IReadOnlyList<Port> outputs = executable.Spec.Outputs;
            steps.Add(new MafStep(
                id,
                executable.Spec.Label.Value,
                [.. PortRules.RequiringSubmission(outputs, executable.Spec.Spec?.Emission ?? NodeEmission.Repeated)
                    .Select(port => port.Name)],
                new MafStepInvocation(binder),
                new MafStepInputAssembler(plan.Inputs)));

            if (plan.Upstream is { } upstream)
            {
                edges.Add(new MafEdge(upstream, id));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        MafFlowDefinition definition = new(flow.Name, startPorts, steps, edges)
        {
            Description = flow.Description,
        };

        if (definition.Validate() is { Count: > 0 } invalid)
        {
            return [.. invalid];
        }

        return definition;
    }

    private static Dictionary<NodePath, MafStepId> StepIds(
        IReadOnlyList<Entry> executables,
        FlowName name,
        List<Error> errors)
    {
        Dictionary<NodePath, MafStepId> ids = [];
        Dictionary<string, NodePath> used = [];
        IEnumerable<(NodePath Path, string Value)> entries = executables.Select(executable =>
            (executable.Path, string.Join(Segment, executable.Path.Levels.Select(level => level.Value))));
        foreach ((NodePath path, string value) in entries)
        {
            if (!used.TryAdd(value, path))
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"节点 {path} 与 {used[value]} 的步骤标识 {value} 重复。"));
                continue;
            }

            ids[path] = new MafStepId(value);
        }

        return ids;
    }

    private static Dictionary<NodePath, MafStepPlan> Plans(
        Tree tree,
        FlowDefinition flow,
        IReadOnlyList<Entry> executables,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        Dictionary<NodePath, MafStepPlan> plans = [];
        foreach (Entry executable in executables)
        {
            CheckShape(executable, ids, flow.Name, errors);
            List<MafInputPlan> inputs = Inputs(tree, flow, executable, ids, errors);
            HashSet<MafStepId> upstream = [.. inputs.Select(input => input.UpstreamStep).OfType<MafStepId>()];

            if (upstream.Count > 1)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {Label(executable, ids)} 有多个来源步骤，MAF 步骤只接受一个上游。"));
            }

            plans[executable.Path] = new MafStepPlan(inputs, upstream.Count == 1 ? upstream.First() : null);
        }

        return plans;
    }

    private static void CheckShape(Entry executable, IReadOnlyDictionary<NodePath, MafStepId> ids, FlowName name, List<Error> errors)
    {
        string label = Label(executable, ids);
        NodeTypeShape? shape = executable.Spec.Type?.Shape;

        if (shape is { AwaitsExternalInput: true })
        {
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的节点类型等待外部输入，MAF 步骤不支持。"));
        }

        foreach (InputPort input in executable.Spec.EffectiveInputs)
        {
            if (input.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输入端口 {input.Name} 类型 {input.Type} 不支持，MAF 只传递文本。"));
            }

            if (input.Trigger)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输入端口 {input.Name} 使用触发，MAF 步骤不支持触发门控。"));
            }
        }

        foreach (Port output in executable.Spec.Outputs)
        {
            if (output.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输出端口 {output.Name} 类型 {output.Type} 不支持，MAF 只传递文本。"));
            }

            if (output.Trigger)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输出端口 {output.Name} 使用触发，MAF 步骤不支持触发位。"));
            }
        }
    }

    private static List<MafInputPlan> Inputs(
        Tree tree,
        FlowDefinition flow,
        Entry executable,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        List<MafInputPlan> plans = [];
        foreach (InputPort port in executable.Spec.EffectiveInputs)
        {
            if (port.Role == InputRole.Control)
            {
                continue;
            }

            PortRef? reference = port.IsStatic
                ? executable.Spec.Bindings?.GetValueOrDefault(port.Name)
                : LinkOf(executable.Spec, port.Name);

            if (reference is { } value)
            {
                MafSource? source = ResolveRef(tree, flow, executable, value, ids, errors);
                if (source is { } resolved)
                {
                    AddPlan(port, resolved, flow, plans, errors, Label(executable, ids));
                }
            }
            else if (port.Default is { Length: > 0 } text)
            {
                plans.Add(MafInputPlan.Constant(port, text));
            }
            else
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"步骤 {Label(executable, ids)} 的输入 {port.Name} 没有来源。"));
            }
        }

        return plans;
    }

    private static PortRef? LinkOf(NodeSpec node, PortName port) =>
        node.From.SelectMany(group => group.Links)
            .FirstOrDefault(link => link.Input == port)?.Ref;

    private static void AddPlan(
        InputPort port,
        MafSource source,
        FlowDefinition flow,
        List<MafInputPlan> plans,
        List<Error> errors,
        string label)
    {
        if (source.IsStart)
        {
            if (source.Port is { } start && flow.StartPorts.Find(start) is { Value: { Length: > 0 } value })
            {
                plans.Add(MafInputPlan.Constant(port, value));
                return;
            }

            if (source.Port is { } startPort)
            {
                plans.Add(MafInputPlan.Start(port, startPort));
                return;
            }
        }

        if (source.Port is { } sourcePort && source.Step is { } step)
        {
            plans.Add(MafInputPlan.Upstream(port, step, sourcePort));
            return;
        }

        errors.Add(FlowErrors.Body(flow.Name.Value, $"步骤 {label} 的输入 {port.Name} 来源不是执行步骤端口。"));
    }

    private static MafSource? ResolveRef(
        Tree tree,
        FlowDefinition flow,
        Entry referrer,
        PortRef reference,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        if (reference.IsStart)
        {
            return flow.StartPorts.Find(reference.Port) is null
                ? MissingStart(referrer, reference.Port, flow.Name, errors)
                : new MafSource(null, reference.Port, null);
        }

        if (reference.Source is not { } label)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, $"节点 {referrer.Path} 的引用 {reference.Display} 没有绑定来源。"));
            return null;
        }

        Entry? source = Resolve(tree, referrer, label, flow.Name, errors);

        return source is null ? null : ToStepSource(tree, flow, source, reference.Port, ids, errors);
    }

    private static MafSource? ToStepSource(
        Tree tree,
        FlowDefinition flow,
        Entry source,
        PortName? port,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        if (source.Spec.Spec is not null && !source.Spec.IsContainer)
        {
            if (port is not { } value || !ids.TryGetValue(source.Path, out MafStepId step))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"来源节点 {source.Path} 不是执行步骤端口。"));
                return null;
            }

            return new MafSource(source.Path, value, step);
        }

        if (!source.Spec.IsContainer)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, $"来源节点 {source.Path} 不是可执行节点。"));
            return null;
        }

        if (port is not { } containerPort)
        {
            List<Entry> members = [.. Descendants(source).Where(entry => entry.Spec.Spec is not null && !entry.Spec.IsContainer)];
            if (members.Count != 1)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"容器 {source.Path} 有 {members.Count} 个执行成员，MAF 步骤不支持成员汇合。"));
                return null;
            }

            return ToStepSource(tree, flow, members[0], null, ids, errors);
        }

        if (source.Spec.Outputs.Find(containerPort) is not { Ref: { } reference })
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, $"容器 {source.Path} 没有输出端口 {containerPort} 的来源映射。"));
            return null;
        }

        if (reference.Source is { } memberLabel)
        {
            Entry? member = ResolveIn(source.Children, memberLabel, flow.Name, errors);
            return member is null ? null : ToStepSource(tree, flow, member, reference.Port, ids, errors);
        }

        if (reference.IsStart)
        {
            return flow.StartPorts.Find(reference.Port) is null
                ? MissingStart(source, reference.Port, flow.Name, errors)
                : new MafSource(null, reference.Port, null);
        }

        if (source.Spec.Bindings?.GetValueOrDefault(reference.Port) is { } binding)
        {
            return ResolveRef(tree, flow, source, binding, ids, errors);
        }

        if (LinkOf(source.Spec, reference.Port) is { } link)
        {
            return ResolveRef(tree, flow, source, link, ids, errors);
        }

        errors.Add(FlowErrors.Body(flow.Name.Value, $"容器 {source.Path} 的输入 {reference.Port} 没有外部来源。"));
        return null;
    }

    private static Entry? Resolve(Tree tree, Entry referrer, NodeLabel label, FlowName name, List<Error> errors)
    {
        Entry? scope = referrer.Parent;
        while (true)
        {
            Entry? found = ResolveIn(scope?.Children ?? tree.Roots, label, name, errors);
            if (found is not null)
            {
                return found;
            }

            if (scope is null)
            {
                errors.Add(FlowErrors.Body(name.Value, $"节点 {referrer.Path} 引用的节点 {label} 不在流程里。"));
                return null;
            }

            scope = scope.Parent;
        }
    }

    private static Entry? ResolveIn(IReadOnlyList<Entry> children, NodeLabel label, FlowName name, List<Error> errors)
    {
        Entry? direct = children.FirstOrDefault(child => child.Spec.Label == label);
        if (direct is not null)
        {
            return direct;
        }

        List<Entry> deeper = [.. children.SelectMany(Descendants).Where(entry => entry.Spec.Label == label)];
        if (deeper.Count > 1)
        {
            errors.Add(FlowErrors.Body(name.Value, $"引用 {label} 在作用域里命中多个节点。"));
            return null;
        }

        return deeper.Count == 1 ? deeper[0] : null;
    }

    private static MafSource? MissingStart(Entry referrer, PortName port, FlowName name, List<Error> errors)
    {
        errors.Add(FlowErrors.Body(name.Value, $"节点 {referrer.Path} 引用的流程开始端口 {port} 不在流程里。"));
        return null;
    }

    private static IEnumerable<Entry> Descendants(Entry entry) =>
        entry.Children.SelectMany(child => new[] { child }.Concat(Descendants(child)));

    private static List<MafStartPort> StartPorts(FlowDefinition flow, List<Error> errors)
    {
        List<MafStartPort> ports = [];
        HashSet<PortName> names = [];
        foreach (Port port in flow.StartPorts)
        {
            if (!names.Add(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 重复。"));
                continue;
            }

            if (port.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 类型 {port.Type} 不支持，MAF 只传递文本。"));
                continue;
            }

            if (port.Trigger)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 使用触发，MAF 步骤不支持触发门控。"));
                continue;
            }

            if (port.Name == MafFlowDefinition.ModelPort)
            {
                if (port.Value is { Length: > 0 })
                {
                    errors.Add(FlowErrors.Body(flow.Name.Value,
                        $"开始端口 {port.Name} 不能是固定值，MAF 由提交给出模型。"));
                    continue;
                }

                ports.Add(new MafStartPort(port.Name, port.Default));
                continue;
            }

            if (port.Mode == PortMode.Constant && port.Value is { Length: > 0 })
            {
                continue;
            }

            ports.Add(new MafStartPort(port.Name, port.Default));
        }

        if (ports.All(port => port.Name != MafFlowDefinition.ModelPort))
        {
            ports.Add(new MafStartPort(MafFlowDefinition.ModelPort));
        }

        return ports;
    }

    private static string Label(Entry executable, IReadOnlyDictionary<NodePath, MafStepId> ids) =>
        ids.TryGetValue(executable.Path, out MafStepId id) ? id.Value : executable.Path.Value;

    private sealed class Entry(NodePath path, NodeSpec spec, Entry? parent)
    {
        public NodePath Path { get; } = path;
        public NodeSpec Spec { get; } = spec;
        public Entry? Parent { get; } = parent;
        public List<Entry> Children { get; } = [];
    }

    private sealed class Tree
    {
        private Tree(List<Entry> roots, List<Entry> entries)
        {
            Roots = roots;
            Entries = entries;
        }

        public IReadOnlyList<Entry> Roots { get; }
        public IReadOnlyList<Entry> Entries { get; }

        public static Tree Build(NodeSpec root)
        {
            List<Entry> roots = [];
            List<Entry> entries = [];
            Add(root, null, roots, entries);

            return new Tree(roots, entries);
        }

        private static void Add(NodeSpec spec, Entry? parent, List<Entry> roots, List<Entry> entries)
        {
            NodePath path = parent is null ? NodePath.Of(spec.Label.Value) : parent.Path.Append(spec.Label);
            Entry entry = new(path, spec, parent);
            (parent?.Children ?? roots).Add(entry);
            entries.Add(entry);
            foreach (NodeSpec child in spec.Nodes ?? [])
            {
                Add(child, entry, roots, entries);
            }
        }
    }
}

/// <summary>绑定来源的解析结果：执行节点端口、容器来源或流程开始端口。</summary>
internal readonly record struct MafSource(NodePath? Node, PortName? Port, MafStepId? Step)
{
    public bool IsStart => Node is null && Port is not null;
}
