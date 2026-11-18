using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Compilation.Graph;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Compilation.Validation;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
internal static class FlowRules
{
    /// <summary>校验一条流程并给出执行视图：流程层规则、图编译与图规则按固定顺序执行。类型规则由节点持有的类型描述承担。</summary>
    internal static ErrorOr<NodeGraph> ValidateAndCompile(FlowDefinition flow)
    {
        List<Error> errors = [];
        ValidateFlow(flow, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        // 引用解析在编译期进行，解析错误与展平后的图规则一并给出
        ErrorOr<NodeGraph> compiled = FlowGraphCompiler.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        ValidateGraph(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);

        return errors.Count > 0 ? errors : compiled.Value;
    }

    /// <summary>流程层规则：开始端口与节点树。引用解析、输入绑定与图规则在具体图上判定。</summary>
    internal static void ValidateFlow(FlowDefinition flow, List<Error> errors)
    {
        CheckStart(flow, errors);
        CheckTree(flow.RootNode, NodeScope.Flow(flow.Name.Value), errors);
    }

    /// <summary>图层规则：环、可达性与边来源。</summary>
    internal static void ValidateGraph(NodeGraph graph, NodeScope scope, List<Error> errors) =>
        CheckShape(graph, scope, errors);

    /// <summary>开始端口的规则：开始端口可以没有。动态端口只能是 Text 或 Signal，常量端口可以使用任意类型。
    /// Default 只用于承载内容的开始端口，是提交输入的预填值与省略时的回退值，不能与 Value 同时使用；不承载内容的常量开始端口必须写 Value。
    /// 承载内容的常量端口没有 Value 与宿主给值时由提交输入给值，宿主给值的端口由宿主开始输入表在提交时确定。</summary>
    private static void CheckStart(FlowDefinition flow, List<Error> errors)
    {
        var names = new HashSet<PortName>();
        foreach (Port port in flow.StartPorts)
        {
            if (!PortTypes.Supports(port.Type, port.Mode))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 的类型 {port.Type} 不支持模式 {port.Mode}。"));
            }

            if (port.Mode != PortMode.Constant
                && port.Type.Semantics is not (PortSemantics.Text or PortSemantics.Signal))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 的动态类型只能是 Text 或 Signal，常量端口可以使用任意类型。"));
            }

            if (!names.Add(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 重复。"));
            }

            if (port.Ref is not null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能写 Ref。"));
            }

            if (port.Trigger)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能使用触发。"));
            }

            if (port.Value is { } value && string.IsNullOrWhiteSpace(value))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Value 不能为空。"));
            }

            if (port.Value is not null && port.Default is not null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 不能同时写 Value 与 Default。"));
            }

            if (port.Default is { } defaultValue && string.IsNullOrWhiteSpace(defaultValue))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Default 不能为空。"));
            }

            if (port.Default is not null && !PortRules.CarriesContent(port.Type))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Default 只用于承载内容的端口。"));
            }

            if (port.IsConstant && !PortRules.CarriesContent(port.Type) && port.Value is null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"不承载内容的常量开始端口 {port.Name} 必须写 Value。"));
            }
        }
    }

    /// <summary>同一容器内的直接子节点标识唯一：重复会让按标识解析的引用落点不定。</summary>
    private static void CheckSiblings(IReadOnlyList<NodeSpec> children, NodeScope scope, List<Error> errors)
    {
        var seen = new HashSet<NodeLabel>();
        foreach (NodeSpec child in children.Where(child => !seen.Add(child.Label)))
        {
            errors.Add(scope.Node(child.Label.Value, "同一容器内的节点标签重复。"));
        }
    }

    /// <summary>递归校验标识、容器形状、输入绑定与统一输出端口。引用解析与图规则在展平后的图上校验。</summary>
    private static void CheckTree(
        NodeSpec node,
        NodeScope scope,
        List<Error> errors)
    {
        // 单节点不变量由节点规则统一判定，这里只补跨节点与阶段规则
        string label = string.IsNullOrWhiteSpace(node.Label.Value) ? "(未命名)" : node.Label.Value;
        node.Check(NodeRuleContext.FlowDefinition, message =>
            errors.Add(scope.Node(label, message)));

        foreach (Port output in node.Outputs.Where(output => output.Default is not null))
        {
            errors.Add(scope.Node(label, $"输出端口 {output.Name} 不能写 Default，Default 只用于开始端口。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (node.Type?.Shape is { Organizes: true })
            {
                node.Type.Validate(node, scope, errors.Add);
            }

            CheckSiblings(children, scope, errors);

            foreach (NodeSpec child in children)
            {
                CheckTree(child, scope, errors);
            }
        }
        else if (node.Spec is not null)
        {
            if (node.Type is not { } definition)
            {
                errors.Add(scope.Node(node.Label.Value, "执行节点必须写类型。"));
            }
            else if (definition.Shape is { Organizes: false })
            {
                definition.Validate(node, scope, errors.Add);
            }
        }
        else if (node.Use is null)
        {
            errors.Add(scope.Node(node.Label.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>输出端口绑定在展平后的图上校验：来源必须是本节点静态输入或子树内成员输出，常量性与类型一致。</summary>
    private static void CheckOutputBindings(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (GraphNode node in graph.Nodes)
        {
            foreach (Port output in node.Outputs)
            {
                if (output.Ref is null)
                {
                    continue;
                }

                if (node.OutputBindings?.TryGetValue(output.Name, out OutputBinding target) != true)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 没有解析到来源。"));
                    continue;
                }

                if (target.Source == node.Path)
                {
                    continue;
                }

                if (!target.Source.IsUnder(node.Path))
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的来源 {target.Source} 不在节点 {node.Path} 的子树里。"));
                    continue;
                }

                GraphNode bound = graph[target.Source];
                if (bound.FindPort(target.Port) is not { } memberPort)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的来源节点 {target.Source} 没有输出端口 {target.Port.Value}。"));
                    continue;
                }

                foreach (string message in OutputSourceRules.Check(
                    output, memberPort.Type, memberPort.IsConstant, $"{target.Source}@{target.Port}"))
                {
                    errors.Add(scope.Node(node.Path.Value, message));
                }

            }
        }
    }

    /// <summary>容器传入端口的绑定：静态输入来自 Bindings，动态输入来自 From；来源必须在容器外部，端口存在，类型一致。
    /// 静态输入沿常量来源解析，动态输入不能绑定常量来源。</summary>
    private static void CheckContainerInputs(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (ContainerNode container in graph.Containers)
        {
            foreach (InputPort input in container.Inputs)
            {
                if (container.InputBindings?.TryGetValue(input.Name, out InputBinding? source) != true
                    || source is not { SourcePort: { } sourcePort }
                    || !AllowedRole(input.IsStatic, source.Role))
                {
                    errors.Add(scope.Node(container.Path.Value, input.IsStatic
                        ? $"容器静态输入端口 {input.Name.Value} 必须由 Bindings 绑定。"
                        : $"容器动态输入端口 {input.Name.Value} 必须由 From 连接。"));
                    continue;
                }

                if (FindPort(graph, source.Source, sourcePort) is not { } sourcePortDeclared)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输入端口 {input.Name.Value} 的来源 {source.Source.Display} 不在流程里。"));
                    continue;
                }

                if (sourcePortDeclared.Type != input.Type)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输入端口 {input.Name.Value} 的类型必须与来源 {source.Source.Display} 一致。"));
                }

                if (input.IsStatic)
                {
                    StaticResolution resolution = StaticSources.ResolveInput(graph, container.Path, input.Name);
                    if (resolution.Error is { } error)
                    {
                        AddResolutionError(scope, resolution, error, errors);
                    }
                }
                else if (sourcePortDeclared.IsConstant)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"动态容器输入端口 {input.Name.Value} 不能连接常量来源 {source.Source.Display}。"));
                }
            }
        }
    }

    /// <summary>容器输入绑定的角色：静态输入只接受 Bindings 产生的绑定，动态输入只接受 From 产生的门控或别名绑定。</summary>
    private static bool AllowedRole(bool isStatic, BindingRole role) =>
        isStatic ? role == BindingRole.Static : role is BindingRole.Gate or BindingRole.Alias;

    /// <summary>静态输入的来源链：链必须到达开始端口、常量输出、字面量或默认值。</summary>
    private static void CheckStaticBindings(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (ExecutableNode node in graph.ExecutableNodes)
        {
            foreach (InputPort input in node.Inputs.Where(port => port.IsStatic))
            {
                StaticResolution resolution = StaticSources.ResolveInput(graph, node.Path, input.Name);
                if (resolution.Error is { } error)
                {
                    AddResolutionError(scope, resolution, error, errors);
                }
            }
        }
    }

    /// <summary>静态解析错误落到流程或来源节点。</summary>
    private static void AddResolutionError(
        NodeScope scope,
        StaticResolution resolution,
        string error,
        List<Error> errors)
    {
        if (resolution.Source.Node is { } node)
        {
            errors.Add(scope.Node(node.Value, error));
        }
        else
        {
            errors.Add(FlowErrors.Body(scope.Name, error));
        }
    }

    /// <summary>按来源取输出端口声明，开始来源取流程端口表，节点来源取节点端口表。</summary>
    private static Port? FindPort(NodeGraph graph, BindingSource source, PortName port)
    {
        if (source.IsStart)
        {
            return graph.StartPorts.Find(port);
        }

        return source.Node is { } node
            ? graph.Nodes.FirstOrDefault(item => item.Path == node)?.FindPort(port)
            : null;
    }

    /// <summary>一条驱动绑定的来源侧校验：来源端口必须声明存在且为动态端口，成员不能被容器外直接引用，来源类型与目标输入一致。</summary>
    private static void CheckEdgeSource(NodeGraph graph, InputBinding binding, ExecutableNode target, NodeScope scope, List<Error> errors)
    {
        if (binding.SourcePort is not { } sourcePort)
        {
            return;
        }

        if (FindPort(graph, binding.Source, sourcePort) is not { } port)
        {
            string message = binding.Source.Node is { } sourceNode
                ? $"节点 {sourceNode} 没有输出端口 {sourcePort.Value}。"
                : $"来源 {binding.Source.Display} 没有输出端口 {sourcePort.Value}。";
            errors.Add(scope.Node(target.Path.Value, message));
            return;
        }

        if (port.IsConstant)
        {
            errors.Add(scope.Node(target.Path.Value,
                $"常量输出端口 {binding.Source.Display} 不进入 From，使用 Bindings 读取。"));
            return;
        }

        if (binding.Type == PortTypes.Context && !SupportsContext(graph, binding.Source, port))
        {
            errors.Add(scope.Node(target.Path.Value,
                $"Context 输入来源 {binding.Source.Display} 必须产出 Context 端口，实际来源端口 {sourcePort.Value} 是 {port.Type}。"));
        }

        if (binding.Source.Node is { } nodePath
            && graph.Nodes.FirstOrDefault(node => node.Path == nodePath) is ExecutableNode executable
            && !Visible(executable.Path, target.Path))
        {
            errors.Add(scope.Node(target.Path.Value,
                $"不能从容器外直接引用容器内成员或端口 {executable.Path}，容器对外只暴露输出端口。"));
        }
    }

    /// <summary>来源是否能提供上下文：开始端口按自身类型，执行节点按类型的输出值域，容器按声明的输出端口。</summary>
    private static bool SupportsContext(NodeGraph graph, BindingSource source, Port port)
    {
        if (source.IsStart)
        {
            return port.Type.Semantics == PortSemantics.Context;
        }

        if (source.Node is not { } nodePath)
        {
            return false;
        }

        return graph.Nodes.FirstOrDefault(node => node.Path == nodePath) switch
        {
            ExecutableNode executable => graph.ShapeOf(executable.Path) is { Organizes: false } shape
                && shape.AllowedOutputTypes.Any(type => type.Semantics == PortSemantics.Context),
            ContainerNode => port.Type.Semantics == PortSemantics.Context,
            _ => false,
        };
    }

    /// <summary>来源是否对目标可见：目标的祖先容器链上有一层直接包含来源，同层成员可见，更深容器的成员对容器外不可见。</summary>
    private static bool Visible(NodePath source, NodePath target) =>
        source.Parent is { } parent && Ancestors(target).Any(container => container == parent);

    /// <summary>目标的祖先容器路径，从直接所属容器起逐层向外。</summary>
    private static IEnumerable<NodePath> Ancestors(NodePath path)
    {
        for (NodePath? current = path.Parent; current is { } container; current = container.Parent)
        {
            yield return container;
        }
    }

    /// <summary>展平后的图规则：见环合法性、从开始的可达性与组间来源端口不重复。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        CheckLoops(graph, scope, errors);
        CheckStaticCycles(graph, scope, errors);
        CheckReachability(graph, scope, errors);
        CheckGroupSources(graph, scope, errors);
        CheckOutputBindings(graph, scope, errors);
        CheckContainerInputs(graph, scope, errors);
        CheckStaticBindings(graph, scope, errors);
        CheckTriggerConditions(graph, scope, errors);

        foreach (InputBinding binding in graph.Bindings)
        {
            if (graph[binding.Target] is not ExecutableNode target)
            {
                continue;
            }

            CheckEdgeSource(graph, binding, target, scope, errors);
        }
    }

    /// <summary>触发条件的来源侧校验：绑定写有非正向条件时，来源端口必须声明触发模式。
    /// 来源节点或端口缺失由来源侧规则报出，这里不重复。</summary>
    private static void CheckTriggerConditions(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (InputBinding binding in graph.Bindings)
        {
            CheckTriggerSource(graph, binding, scope, errors);
        }

        foreach (GraphNode node in graph.Nodes)
        {
            foreach (InputBinding binding in node.InputBindings?.Values ?? [])
            {
                CheckTriggerSource(graph, binding, scope, errors);
            }
        }
    }

    /// <summary>一条绑定的来源端口是否承载触发值，绑定目标按自身节点作用域报错。</summary>
    private static void CheckTriggerSource(
        NodeGraph graph,
        InputBinding binding,
        NodeScope scope,
        List<Error> errors)
    {
        if (binding.Condition == TriggerCondition.Positive
            || binding.SourcePort is not { } sourcePort)
        {
            return;
        }

        if (FindPort(graph, binding.Source, sourcePort) is { Trigger: false })
        {
            errors.Add(scope.Node(binding.Target.Value,
                $"触发条件要求来源 {binding.Source.Display} 是触发端口。"));
        }
    }

    /// <summary>组间来源端口不重复：同一来源端口作为启动条件只能落在一个组里，跨组出现即多组同时齐备。</summary>
    private static void CheckGroupSources(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (IGrouping<(NodePath Target, BindingSource Source, PortName? SourcePort), InputBinding> group in graph.Bindings
            .GroupBy(binding => (binding.Target, binding.Source, binding.SourcePort)))
        {
            if (group.Select(binding => binding.Group).Distinct().Count() < 2
                || graph[group.Key.Target] is not ExecutableNode target)
            {
                continue;
            }

            errors.Add(scope.Node(target.Path.Value, $"From 的来源端口 {group.Key.Source.Display} 出现在多个组里。"));
        }
    }

    /// <summary>可达性规则：开始端口的驱动绑定与全部输入静态且不依赖运行期常量的执行节点构成初始来源；
    /// 其余执行节点必须能从这些初始来源沿引用边或常量依赖到达。容器是成员的汇合点，成员全部可达时容器才可达。</summary>
    private static void CheckReachability(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var reached = new HashSet<NodePath>(graph.StartOutgoing.Select(binding => binding.Target));
        var queue = new Queue<NodePath>(reached);
        foreach (ExecutableNode executable in graph.ExecutableNodes
            .Where(node => graph.PlanOf(node.Path) is { StaticOnly: true })
            .Where(node => !graph.StaticBindings.Any(binding => binding.Target == node.Path))
            .Where(node => reached.Add(node.Path)))
        {
            queue.Enqueue(executable.Path);
        }

        while (true)
        {
            // reached.Add 兼作去重判定：首次到达的节点才入队
            while (queue.TryDequeue(out NodePath current))
            {
                foreach (NodePath next in graph.Outgoing(current).Select(binding => binding.Target)
                    .Concat(graph.StaticBindings
                        .Where(binding => binding.Source.Node == current)
                        .Select(binding => binding.Target))
                    .Where(reached.Add))
                {
                    queue.Enqueue(next);
                }
            }

            ContainerNode? closed = graph.Containers.FirstOrDefault(container =>
                !reached.Contains(container.Path)
                && container.Children.All(child => reached.Contains(child.Path)));
            if (closed is null)
            {
                break;
            }

            reached.Add(closed.Path);
            queue.Enqueue(closed.Path);
        }

        string entry = graph.StartPorts
            .Where(port => port.Value is null)
            .Select(port => $" @{port.Name}")
            .FirstOrDefault() ?? " 开始端口";
        foreach (ExecutableNode executable in graph.ExecutableNodes.Where(node => !reached.Contains(node.Path)))
        {
            errors.Add(scope.Node(executable.Path.Value,
                $"从开始不可达，不会被触发，请用 From 接到来源节点或把静态输入绑定到常量来源与{entry}。"));
        }
    }

    /// <summary>常量依赖环：静态输入沿常量来源逐级等待，形成环时环内节点永远等不到值。</summary>
    private static void CheckStaticCycles(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        Dictionary<NodePath, List<NodePath>> outgoing = [];
        foreach (InputBinding binding in graph.StaticBindings)
        {
            if (binding.Source.Node is not { } source)
            {
                continue;
            }

            if (!outgoing.TryGetValue(source, out List<NodePath>? targets))
            {
                targets = [];
                outgoing[source] = targets;
            }

            if (!targets.Contains(binding.Target))
            {
                targets.Add(binding.Target);
            }
        }

        Dictionary<NodePath, int> incoming = graph.ExecutableNodes.ToDictionary(node => node.Path, _ => 0);
        foreach (InputBinding binding in graph.StaticBindings
            .Where(binding => incoming.ContainsKey(binding.Target)))
        {
            incoming[binding.Target]++;
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
            errors.Add(FlowErrors.Body(scope.Name, $"常量依赖形成环：{members}。"));
        }
    }

    /// <summary>真实依赖环的强连通分量：来源容器展开到全部成员后建邻接，只取成员多于一个或带自连环的分量。</summary>
    private static List<NodePath[]> Loops(NodeGraph graph)
    {
        ExecutableNode[] executables = [.. graph.ExecutableNodes];
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

        foreach (InputBinding binding in graph.Bindings)
        {
            if (binding.Source.Node is not { } sourcePath)
            {
                continue;
            }

            foreach (NodePath source in graph.ExecutablesIn(sourcePath))
            {
                adjacent[rank[source]].Add(rank[binding.Target]);
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

    /// <summary>引用环的启动不变量：环必须有环外来源或环内输入节点，否则环内节点永远等不到启动条件，任务无法推进。
    /// 开始端口是流程级来源，输入节点每轮从外部接收回答，两者都构成周期性外源。节点依赖构成的迭代环若可启动则合法。</summary>
    private static void CheckLoops(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (InputBinding binding in graph.Bindings)
        {
            // 成员从自身所在容器取输入会让容器永远等不到齐备，是启动条件永不满足的环
            if (binding.Source.Node is { } sourcePath
                && graph[sourcePath] is ContainerNode container
                && graph.ExecutablesIn(container.Path).Contains(binding.Target))
            {
                errors.Add(scope.Node(graph[binding.Target].Path.Value,
                    "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"));
            }
        }

        foreach (NodePath[] members in Loops(graph))
        {
            // 环内可自启动的节点是挂点也是周期外源，无需环外来源即可启动
            if (members.Any(member => graph.ShapeOf(member) is { StartsUnaided: true }))
            {
                continue;
            }

            bool externalEntry = members.Any(member =>
                graph.Incoming(member).Any(binding =>
                    binding.Source.IsStart
                    || binding.Source.Node is { } sourcePath
                    && graph.ExecutablesIn(sourcePath).Any(source => !members.Contains(source))));
            if (!externalEntry)
            {
                ExecutableNode representative = (ExecutableNode)graph[members[0]];
                errors.Add(scope.Node(representative.Path.Value,
                    "引用环没有环外来源且环内没有输入节点，任务无法启动。"));
            }
        }
    }
}
