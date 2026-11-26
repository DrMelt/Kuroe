using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Semantics;

/// <summary>流程语言语义解释：按容器作用域解析引用，沿绑定链求来源终点，映射容器输出并给出来源诊断。</summary>
public sealed class FlowSemanticsAnalyzer : IFlowSemanticsAnalyzer
{
    /// <inheritdoc/>
    public FlowSemantics Analyze(FlowDefinition flow) => new Context(flow).Analyze();

    private sealed class Context
    {
        private readonly FlowDefinition _flow;
        private readonly List<Entry> _roots = [];
        private readonly List<Entry> _entries = [];
        private readonly List<FlowSemanticNode> _nodes = [];
        private readonly List<FlowSemanticDiagnostic> _diagnostics = [];
        private readonly HashSet<string> _seen = [];
        private readonly Dictionary<NodePath, Entry> _entryByPath = [];
        private readonly Dictionary<NodePath, FlowSemanticNode> _nodeByPath = [];

        internal Context(FlowDefinition flow)
        {
            _flow = flow;
            Build(flow.RootNode, null);
        }

        internal FlowSemantics Analyze()
        {
            foreach (Entry entry in _entries)
            {
                _entryByPath[entry.Path] = entry;
                _nodes.Add(Result(entry));
            }

            foreach (FlowSemanticNode node in _nodes)
            {
                _nodeByPath[node.Path] = node;
            }

            if (_diagnostics.Count == 0)
            {
                Validate();
            }

            return new FlowSemantics(_flow, _nodes, _diagnostics);
        }

        private FlowSemanticNode Result(Entry entry)
        {
            Dictionary<PortName, FlowSemanticOutput> outputs = [];
            foreach (Port port in entry.Spec.Outputs)
            {
                outputs[port.Name] = new FlowSemanticOutput(
                    port,
                    ResolveOutputBinding(entry, port),
                    ResolveOutput(entry, port.Name, []));
            }

            List<FlowSemanticInput> inputs = [];
            foreach (InputPort port in entry.Spec.EffectiveInputs)
            {
                if (port.IsStatic)
                {
                    ResolvedRef resolved = ResolveStaticInput(entry, port, []);
                    inputs.Add(new FlowSemanticInput(
                        port,
                        Group: -1,
                        TriggerCondition.Positive,
                        resolved.Binding,
                        resolved.Source));
                    continue;
                }

                List<FlowSemanticInput> linked = Linked(entry, port);
                if (linked.Count > 0)
                {
                    inputs.AddRange(linked);
                    continue;
                }

                FlowSource fallback;
                if (port.Default is { Length: > 0 } text)
                {
                    fallback = FlowSource.Default(text);
                }
                else if (port.Role == InputRole.Control)
                {
                    fallback = FlowSource.Missing();
                }
                else
                {
                    fallback = Missing(entry.Path, $"动态输入端口 {port.Name} 没有来源。");
                }

                inputs.Add(new FlowSemanticInput(
                    port,
                    Group: -1,
                    TriggerCondition.Positive,
                    Binding: null,
                    fallback));
            }

            return new FlowSemanticNode(entry.Path, entry.Spec, inputs, outputs);
        }

        private List<FlowSemanticInput> Linked(Entry entry, InputPort port)
        {
            List<FlowSemanticInput> inputs = [];
            for (int group = 0; group < entry.Spec.From.Count; group++)
            {
                foreach (FromLink link in entry.Spec.From[group].Links.Where(link => link.Input == port.Name))
                {
                    ResolvedRef resolved = ResolveRef(entry, link.Ref, []);
                    inputs.Add(new FlowSemanticInput(
                        port,
                        group,
                        link.Condition,
                        resolved.Binding,
                        resolved.Source));
                }
            }

            return inputs;
        }

        private ResolvedRef ResolveStaticInput(Entry owner, InputPort port, HashSet<PortKey> visiting)
        {
            ResolvedRef resolved;
            if (owner.Spec.Bindings?.TryGetValue(port.Name, out PortRef reference) == true)
            {
                resolved = ResolveRef(owner, reference, visiting);
            }
            else if (port.Default is { Length: > 0 } text)
            {
                resolved = new ResolvedRef(null, FlowSource.Default(text));
            }
            else
            {
                return new ResolvedRef(null,
                    Missing(owner.Path, $"静态输入端口 {port.Name} 没有绑定来源或默认值。"));
            }

            if (resolved.Source.Kind is FlowSourceKind.NodeOutput)
            {
                return resolved with
                {
                    Source = Missing(owner.Path,
                        $"节点 {resolved.Source.Node} 的输出端口 {resolved.Source.Port} 不是常量端口。"),
                };
            }

            if (resolved.Source.Kind is FlowSourceKind.StartInput
                && resolved.Source.Port is { } startPort
                && _flow.StartPorts.Find(startPort) is { IsConstant: false })
            {
                return resolved with
                {
                    Source = Missing(owner.Path,
                        $"静态输入端口 {port.Name} 绑定的开始端口 {startPort} 不是常量端口。"),
                };
            }

            return resolved;
        }

        private ResolvedRef ResolveRef(Entry referrer, PortRef reference, HashSet<PortKey> visiting)
        {
            if (reference.IsStart)
            {
                FlowBinding? binding = _flow.StartPorts.Find(reference.Port) is null
                    ? null
                    : FlowBinding.Start(reference.Port);

                return new ResolvedRef(binding, ResolveStart(referrer, reference.Port));
            }

            if (reference.Source is not { } label)
            {
                return new ResolvedRef(null,
                    Missing(referrer.Path, $"引用 {reference.Display} 没有绑定来源。"));
            }

            Entry? source = Resolve(referrer, label);
            if (source is null)
            {
                return new ResolvedRef(null, FlowSource.Missing());
            }

            if (source.Spec.Outputs.Find(reference.Port) is null)
            {
                NodePath path = source.Spec.IsContainer ? source.Path : referrer.Path;

                return new ResolvedRef(
                    FlowBinding.ForNode(source.Path, reference.Port),
                    Missing(path, $"节点 {source.Path} 没有输出端口 {reference.Port}。"));
            }

            return new ResolvedRef(
                FlowBinding.ForNode(source.Path, reference.Port),
                ResolveOutput(source, reference.Port, visiting));
        }

        private FlowSource ResolveStart(Entry referrer, PortName port)
        {
            if (_flow.StartPorts.Find(port) is not { } declared)
            {
                return Missing(referrer.Path, $"引用的流程开始端口 {port} 不在流程里。");
            }

            return declared.IsConstant && declared.Value is { Length: > 0 } text
                ? FlowSource.StartFixed(port, text)
                : FlowSource.StartInput(port);
        }

        private FlowSource ResolveOutput(Entry node, PortName port, HashSet<PortKey> visiting)
        {
            PortKey key = new(node.Path, port, Output: true);
            if (!visiting.Add(key))
            {
                return Missing(node.Path, $"输出端口 {port} 的来源形成环。");
            }

            try
            {
                if (node.Spec.Outputs.Find(port) is not { } output)
                {
                    return Missing(node.Path, $"节点 {node.Path} 没有输出端口 {port}。");
                }

                if (node.Spec.IsContainer)
                {
                    if (output.Value is { Length: > 0 } containerText)
                    {
                        return FlowSource.NodeConstant(node.Path, port, containerText);
                    }

                    return output.Ref is { } containerRef
                        ? ResolveOutputRef(node, port, containerRef, visiting)
                        : Missing(node.Path, $"容器 {node.Path} 的输出端口 {port} 没有来源映射。");
                }

                if (output.IsConstant)
                {
                    if (output.Value is { Length: > 0 } text)
                    {
                        return FlowSource.NodeConstant(node.Path, port, text);
                    }

                    if (output.Ref is { } reference)
                    {
                        return ResolveOutputRef(node, port, reference, visiting);
                    }

                    return node.Spec.Spec?.Emission == NodeEmission.Once
                        ? FlowSource.RuntimeConstant(node.Path, port)
                        : Missing(node.Path, $"常量输出端口 {port} 没有固定值、来源或运行期产出。");
                }

                return FlowSource.NodeOutput(node.Path, port);
            }
            finally
            {
                visiting.Remove(key);
            }
        }

        private FlowBinding? ResolveOutputBinding(Entry owner, Port port)
        {
            if (port.Ref is not { } reference)
            {
                return null;
            }

            if (reference.Source is not { } label)
            {
                return FlowBinding.ForNode(owner.Path, reference.Port);
            }

            Entry? member = ResolveIn(owner.Children, label, owner.Path);

            return member is null ? null : FlowBinding.ForNode(member.Path, reference.Port);
        }

        private FlowSource ResolveOutputRef(
            Entry owner,
            PortName output,
            PortRef reference,
            HashSet<PortKey> visiting)
        {
            if (reference.Source is not { } label)
            {
                InputPort? input = owner.Spec.FindInput(reference.Port);

                return input is null
                    ? Missing(owner.Path, $"输出端口引用的输入端口 {reference.Port} 没有在节点上声明。")
                    : ResolveInputSource(owner, input.Value, visiting).Source;
            }

            Entry? member = ResolveIn(owner.Children, label, owner.Path);
            if (member is null)
            {
                return Missing(owner.Path, $"输出端口的来源 {label} 不在节点 {owner.Path} 的子树里。");
            }

            if (member.Spec.Outputs.Find(reference.Port) is null)
            {
                return Missing(owner.Path,
                    $"输出端口 {output} 的来源节点 {member.Path} 没有输出端口 {reference.Port}。");
            }

            return ResolveOutput(member, reference.Port, visiting);
        }

        private ResolvedRef ResolveInputSource(Entry owner, InputPort input, HashSet<PortKey> visiting)
        {
            if (input.IsStatic)
            {
                return ResolveStaticInput(owner, input, visiting);
            }

            FromLink? link = owner.Spec.From
                .SelectMany(group => group.Links)
                .FirstOrDefault(candidate => candidate.Input == input.Name);

            if (link is { } found)
            {
                return ResolveRef(owner, found.Ref, visiting);
            }

            if (input.Default is { Length: > 0 } text)
            {
                return new ResolvedRef(null, FlowSource.Default(text));
            }

            return new ResolvedRef(null, Missing(owner.Path, $"输入端口 {input.Name} 没有来源。"));
        }

        private Entry? Resolve(Entry referrer, NodeLabel label)
        {
            Entry? scope = referrer.Parent;
            while (true)
            {
                Entry? found = ResolveIn(scope?.Children ?? _roots, label, referrer.Path);
                if (found is not null)
                {
                    return found;
                }

                if (scope is null)
                {
                    Missing(referrer.Path, $"引用的节点 {label} 不在流程里。");
                    return null;
                }

                scope = scope.Parent;
            }
        }

        private Entry? ResolveIn(IReadOnlyList<Entry> children, NodeLabel label, NodePath owner)
        {
            Entry? direct = children.FirstOrDefault(child => child.Spec.Label == label);
            if (direct is not null)
            {
                return direct;
            }

            List<Entry> deeper = [.. children.SelectMany(Descendants).Where(entry => entry.Spec.Label == label)];
            if (deeper.Count > 1)
            {
                Missing(owner, $"引用 {label} 在作用域里命中多个节点。");
                return null;
            }

            return deeper.Count == 1 ? deeper[0] : null;
        }

        private IEnumerable<Entry> Descendants(Entry entry) =>
            entry.Children.SelectMany(child => new[] { child }.Concat(Descendants(child)));

        private FlowSource Missing(NodePath path, string message)
        {
            AddDiagnostic(path, message);
            return FlowSource.Missing();
        }

        private void Validate()
        {
            CheckLoops();
            CheckStaticCycles();
            CheckReachability();
            CheckGroupSources();
            CheckOutputBindings();
            CheckContainerInputs();
            CheckTriggerConditions();
            CheckEdgeSources();
        }

        private Port? DeclaredPort(FlowBinding binding)
        {
            if (binding.IsStart)
            {
                return _flow.StartPorts.Find(binding.Port);
            }

            return binding.Node is { } node
                ? _nodeByPath.GetValueOrDefault(node)?.FindOutput(binding.Port)?.Port
                : null;
        }

        private bool SupportsContext(FlowBinding binding)
        {
            if (DeclaredPort(binding) is not { } port)
            {
                return false;
            }

            if (binding.IsStart)
            {
                return port.Type.Semantics == PortSemantics.Context;
            }

            if (binding.Node is not { } path || _nodeByPath.GetValueOrDefault(path) is not { } node)
            {
                return false;
            }

            return node.Spec.IsContainer
                ? port.Type.Semantics == PortSemantics.Context
                : node.Spec.Type?.Shape is { Organizes: false } shape
                    && shape.AllowedOutputTypes.Any(type => type.Semantics == PortSemantics.Context);
        }

        private IEnumerable<NodePath> ExecutablesIn(NodePath source)
        {
            if (!_entryByPath.TryGetValue(source, out Entry? entry))
            {
                yield break;
            }

            if (entry.Spec.Spec is not null && !entry.Spec.IsContainer)
            {
                yield return entry.Path;
                yield break;
            }

            foreach (Entry child in entry.Children)
            {
                foreach (NodePath path in ExecutablesIn(child.Path))
                {
                    yield return path;
                }
            }
        }

        private static IEnumerable<NodePath> Ancestors(NodePath path)
        {
            for (NodePath? current = path.Parent; current is { } container; current = container.Parent)
            {
                yield return container;
            }
        }

        private static bool Visible(NodePath source, NodePath target) =>
            source.Parent is { } parent && Ancestors(target).Any(container => container == parent);

        private void CheckLoops()
        {
            foreach (FlowSemanticNode node in _nodes.Where(node => node.IsExecutable))
            {
                foreach (FlowSemanticInput input in node.Inputs.Where(input => input.IsLinked && input.Binding is not null))
                {
                    FlowBinding binding = input.Binding!.Value;
                    if (binding.Node is { } source
                        && _entryByPath.GetValueOrDefault(source) is { } container
                        && container.Spec.IsContainer
                        && node.Path.IsUnder(source))
                    {
                        AddDiagnostic(node.Path, "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。");
                    }
                }
            }

            List<FlowSemanticNode> executables = [.. _nodes.Where(node => node.IsExecutable)];
            var rank = new Dictionary<NodePath, int>();
            for (int i = 0; i < executables.Count; i++)
            {
                rank[executables[i].Path] = i;
            }

            var adjacent = new List<List<int>>(executables.Count);
            for (int i = 0; i < executables.Count; i++)
            {
                adjacent.Add([]);
            }

            foreach (FlowSemanticNode target in executables)
            {
                foreach (FlowSemanticInput input in target.Inputs.Where(input => input.IsLinked && input.Binding is not null))
                {
                    if (input.Binding!.Value.Node is not { } source)
                    {
                        continue;
                    }

                    foreach (NodePath member in ExecutablesIn(source))
                    {
                        adjacent[rank[member]].Add(rank[target.Path]);
                    }
                }
            }

            foreach (NodePath[] members in LoopMembers(executables, adjacent))
            {
                if (members.Any(member => _nodeByPath[member].Spec.Type?.Shape is { StartsUnaided: true }))
                {
                    continue;
                }

                bool externalEntry = members.Any(member =>
                    _nodeByPath[member].Inputs.Any(input =>
                        input.IsLinked
                        && input.Binding is { } binding
                        && (binding.IsStart
                            || binding.Node is { } source
                            && ExecutablesIn(source).Any(executable => !members.Contains(executable)))));
                if (!externalEntry)
                {
                    AddDiagnostic(members[0], "引用环没有环外来源且环内没有输入节点，任务无法启动。");
                }
            }
        }

        private static List<NodePath[]> LoopMembers(
            List<FlowSemanticNode> executables,
            List<List<int>> adjacent)
        {
            var loops = new List<NodePath[]>();
            var index = new int[executables.Count];
            var low = new int[executables.Count];
            Array.Fill(index, -1);
            var stack = new Stack<int>();
            var onStack = new bool[executables.Count];
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

            for (int start = 0; start < executables.Count; start++)
            {
                if (index[start] == -1)
                {
                    Tarjan(start);
                }
            }

            return loops;
        }

        private void CheckStaticCycles()
        {
            List<FlowSemanticNode> executables = [.. _nodes.Where(node => node.IsExecutable)];
            Dictionary<NodePath, List<NodePath>> outgoing = [];
            foreach (FlowSemanticNode node in executables)
            {
                foreach (FlowSemanticInput input in node.Inputs.Where(input =>
                    input.Port.IsStatic && input.Source.Kind == FlowSourceKind.RuntimeConstant))
                {
                    if (input.Source.Node is not { } source)
                    {
                        continue;
                    }

                    if (!outgoing.TryGetValue(source, out List<NodePath>? targets))
                    {
                        targets = [];
                        outgoing[source] = targets;
                    }

                    if (!targets.Contains(node.Path))
                    {
                        targets.Add(node.Path);
                    }
                }
            }

            Dictionary<NodePath, int> incoming = executables.ToDictionary(node => node.Path, _ => 0);
            foreach (FlowSemanticNode node in executables)
            {
                incoming[node.Path] += node.Inputs.Count(input =>
                    input.Port.IsStatic && input.Source.Kind == FlowSourceKind.RuntimeConstant);
            }

            Queue<NodePath> ready = new([.. incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key)]);
            int resolved = 0;
            while (ready.TryDequeue(out NodePath current))
            {
                resolved++;
                foreach (NodePath next in outgoing.GetValueOrDefault(current) ?? [])
                {
                    if (incoming.TryGetValue(next, out int count))
                    {
                        count--;
                        incoming[next] = count;
                        if (count == 0)
                        {
                            ready.Enqueue(next);
                        }
                    }
                }
            }

            if (resolved < incoming.Count)
            {
                string members = string.Join('、', incoming.Where(pair => pair.Value > 0).Select(pair => pair.Key.Value));
                AddDiagnostic(_entries[0].Path, $"常量依赖形成环：{members}。");
            }
        }

        private void CheckReachability()
        {
            List<FlowSemanticNode> executables = [.. _nodes.Where(node => node.IsExecutable)];
            var reached = new HashSet<NodePath>();
            var queue = new Queue<NodePath>();

            foreach (NodePath path in executables
                .Where(node => node.Inputs.Any(input => input.IsLinked && input.Binding?.IsStart == true))
                .Select(node => node.Path)
                .Where(reached.Add))
            {
                queue.Enqueue(path);
            }

            foreach (FlowSemanticNode node in executables
                .Where(node => node.Inputs.All(input => input.Port.IsStatic))
                .Where(node => !node.Inputs.Any(input =>
                    input.Port.IsStatic && input.Source.Kind == FlowSourceKind.RuntimeConstant))
                .Where(node => reached.Add(node.Path)))
            {
                queue.Enqueue(node.Path);
            }

            while (true)
            {
                while (queue.TryDequeue(out NodePath current))
                {
                    foreach (NodePath next in Next(current).Where(reached.Add))
                    {
                        queue.Enqueue(next);
                    }
                }

                Entry? closed = _entries
                    .Where(entry => entry.Spec.IsContainer && !reached.Contains(entry.Path))
                    .FirstOrDefault(entry => entry.Children.All(child => reached.Contains(child.Path)));
                if (closed is null)
                {
                    break;
                }

                reached.Add(closed.Path);
                queue.Enqueue(closed.Path);
            }

            string entry = _flow.StartPorts
                .Where(port => port.Value is null)
                .Select(port => $" @{port.Name}")
                .FirstOrDefault() ?? " 开始端口";
            foreach (FlowSemanticNode node in executables.Where(node => !reached.Contains(node.Path)))
            {
                AddDiagnostic(node.Path,
                    $"从开始不可达，不会被触发，请用 From 接到来源节点或把静态输入绑定到常量来源与{entry}。");
            }

            IEnumerable<NodePath> Next(NodePath source)
            {
                foreach (FlowSemanticNode node in executables)
                {
                    foreach (FlowSemanticInput input in node.Inputs)
                    {
                        NodePath? origin = null;
                        if (input.IsLinked)
                        {
                            origin = input.Binding?.Node;
                        }
                        else if (input.Port.IsStatic && input.Source.Kind == FlowSourceKind.RuntimeConstant)
                        {
                            origin = input.Source.Node;
                        }

                        if (origin != source)
                        {
                            continue;
                        }

                        yield return node.Path;
                    }
                }
            }
        }

        private void CheckGroupSources()
        {
            foreach (FlowSemanticNode node in _nodes.Where(node => node.IsExecutable))
            {
                foreach (IGrouping<FlowBinding, FlowSemanticInput> group in node.Inputs
                    .Where(input => input.IsLinked && input.Binding is not null)
                    .GroupBy(input => input.Binding!.Value))
                {
                    if (group.Select(input => input.Group).Distinct().Count() < 2)
                    {
                        continue;
                    }

                    AddDiagnostic(node.Path, $"From 的来源端口 {group.Key.Display} 出现在多个组里。");
                }
            }
        }

        private void CheckOutputBindings()
        {
            foreach (FlowSemanticNode node in _nodes)
            {
                foreach (FlowSemanticOutput output in node.Outputs.Values.Where(output => output.Port.Ref is not null))
                {
                    if (output.Binding is not { } binding)
                    {
                        AddDiagnostic(node.Path, $"输出端口 {output.Port.Name.Value} 没有解析到来源。");
                        continue;
                    }

                    if (binding.Node == node.Path)
                    {
                        continue;
                    }

                    if (binding.Node is not { } source
                        || !_nodeByPath.TryGetValue(source, out FlowSemanticNode? member)
                        || member.FindOutput(binding.Port) is not { } memberOutput)
                    {
                        continue;
                    }

                    foreach (string message in OutputSourceRules.Check(
                        output.Port,
                        memberOutput.Port.Type,
                        memberOutput.Port.IsConstant,
                        binding.Display))
                    {
                        AddDiagnostic(node.Path, message);
                    }
                }
            }
        }

        private void CheckContainerInputs()
        {
            foreach (Entry entry in _entries.Where(entry => entry.Spec.IsContainer))
            {
                foreach (InputPort input in entry.Spec.EffectiveInputs)
                {
                    foreach (FlowSemanticInput record in _nodeByPath[entry.Path].Inputs
                        .Where(record => record.Port.Name == input.Name))
                    {
                        if (record.Binding is not { } binding)
                        {
                            continue;
                        }

                        if (binding.Node is { } source && (source == entry.Path || source.IsUnder(entry.Path)))
                        {
                            AddDiagnostic(entry.Path,
                                $"容器输入端口 {input.Name.Value} 的来源 {binding.Display} 必须在容器外部。");
                            continue;
                        }

                        if (DeclaredPort(binding) is not { } sourcePort)
                        {
                            continue;
                        }

                        if (sourcePort.Type != input.Type)
                        {
                            AddDiagnostic(entry.Path,
                                $"容器输入端口 {input.Name.Value} 的类型必须与来源 {binding.Display} 一致。");
                        }

                        if (!input.IsStatic
                            && record.Source.Kind is FlowSourceKind.NodeConstant or FlowSourceKind.RuntimeConstant)
                        {
                            AddDiagnostic(entry.Path,
                                $"动态容器输入端口 {input.Name.Value} 不能连接常量来源 {binding.Display}。");
                        }
                    }
                }
            }
        }

        private void CheckTriggerConditions()
        {
            foreach (FlowSemanticNode node in _nodes)
            {
                foreach (FlowSemanticInput input in node.Inputs.Where(input =>
                    input.IsLinked
                    && input.Condition != TriggerCondition.Positive
                    && input.Binding is not null))
                {
                    FlowBinding binding = input.Binding!.Value;
                    if (DeclaredPort(binding) is { Trigger: false })
                    {
                        AddDiagnostic(node.Path, $"触发条件要求来源 {binding.Display} 是触发端口。");
                    }
                }
            }
        }

        private void CheckEdgeSources()
        {
            foreach (FlowSemanticNode node in _nodes.Where(node => node.IsExecutable))
            {
                foreach (FlowSemanticInput input in node.Inputs.Where(input => input.IsLinked && input.Binding is not null))
                {
                    FlowBinding binding = input.Binding!.Value;
                    if (DeclaredPort(binding) is not { } port)
                    {
                        string message = binding.Node is { } source
                            ? $"节点 {source} 没有输出端口 {binding.Port.Value}。"
                            : $"来源 {binding.Display} 没有输出端口 {binding.Port.Value}。";
                        AddDiagnostic(node.Path, message);
                        continue;
                    }

                    if (port.IsConstant)
                    {
                        AddDiagnostic(node.Path,
                            $"常量输出端口 {binding.Display} 不进入 From，使用 Bindings 读取。");
                        continue;
                    }

                    if (input.Port.Type == PortTypes.Context && !SupportsContext(binding))
                    {
                        AddDiagnostic(node.Path,
                            $"Context 输入来源 {binding.Display} 必须产出 Context 端口，实际来源端口 {binding.Port} 是 {port.Type}。");
                    }

                    if (binding.Node is { } sourcePath
                        && _nodeByPath.GetValueOrDefault(sourcePath) is { } sourceNode
                        && sourceNode.IsExecutable
                        && !Visible(sourcePath, node.Path))
                    {
                        AddDiagnostic(node.Path,
                            $"不能从容器外直接引用容器内成员或端口 {sourcePath}，容器对外只暴露输出端口。");
                    }
                }
            }
        }

        private void AddDiagnostic(NodePath path, string message)
        {
            if (_seen.Add($"{path.Value}|{message}"))
            {
                _diagnostics.Add(new FlowSemanticDiagnostic(path, message));
            }
        }

        private void Build(NodeSpec spec, Entry? parent)
        {
            NodePath path = parent is null ? NodePath.Of(spec.Label.Value) : parent.Path.Append(spec.Label);
            Entry entry = new(path, spec, parent);
            (parent?.Children ?? _roots).Add(entry);
            _entries.Add(entry);
            foreach (NodeSpec child in spec.Nodes ?? [])
            {
                Build(child, entry);
            }
        }
    }

    private sealed class Entry(NodePath path, NodeSpec spec, Entry? parent)
    {
        public NodePath Path { get; } = path;
        public NodeSpec Spec { get; } = spec;
        public Entry? Parent { get; } = parent;
        public List<Entry> Children { get; } = [];
    }

    private readonly record struct PortKey(NodePath Path, PortName Port, bool Output);

    private readonly record struct ResolvedRef(FlowBinding? Binding, FlowSource Source);
}