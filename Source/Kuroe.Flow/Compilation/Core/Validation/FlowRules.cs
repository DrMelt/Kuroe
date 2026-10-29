using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Compilation.Graph;

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

    /// <summary>流程层规则：开始入口与节点树。引用解析、静态绑定与图规则在具体图上判定。</summary>
    internal static void ValidateFlow(FlowDefinition flow, List<Error> errors)
    {
        CheckStart(flow, errors);
        CheckTree(flow.RootNode, NodeScope.Flow(flow.Name.Value), errors);
    }

    /// <summary>图层规则：环、可达性与边来源。</summary>
    internal static void ValidateGraph(NodeGraph graph, NodeScope scope, List<Error> errors) =>
        CheckShape(graph, scope, errors);

    /// <summary>开始入口的规则：至少一个 Text 端口，至少一个不带 Value 的端口承接开始输入；常量端口必须有 Value 或宿主输入绑定。
    /// 宿主输入只能绑定已声明且不带 Value 的端口。</summary>
    private static void CheckStart(FlowDefinition flow, List<Error> errors)
    {
        if (flow.StartOutputs.Count == 0)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, "流程必须声明开始入口，至少一个 Text 输出端口。"));
            return;
        }

        var names = new HashSet<PortName>();
        Dictionary<PortName, Port> declared = [];
        foreach (Port port in flow.StartOutputs)
        {
            if (port.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的类型只能是 Text。"));
            }

            if (!names.Add(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 重复。"));
            }

            declared[port.Name] = port;

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

            if (port.IsConstant && port.Value is null && !flow.HostStartInputs.ContainsKey(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"常量开始端口 {port.Name} 必须有 Value 或宿主输入绑定。"));
            }
        }

        foreach ((PortName port, string input) in flow.HostStartInputs)
        {
            if (string.IsNullOrWhiteSpace(port.Value))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, "宿主开始输入的端口名不能为空。"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port} 的宿主输入不能为空。"));
                continue;
            }

            if (!declared.TryGetValue(port, out Port startPort))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"宿主开始输入 {port} 没有对应的开始端口。"));
                continue;
            }

            if (startPort.Value is not null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port} 带固定值，不能绑定宿主输入。"));
            }
        }

        if (!flow.StartOutputs.Any(port => port.Value is null))
        {
            errors.Add(FlowErrors.Body(flow.Name.Value,
                "开始入口必须至少有一个不带 Value 的端口承接开始输入。"));
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

    /// <summary>递归校验标识、容器形状、静态绑定与统一输出端口。引用解析与图规则在展平后的图上校验。</summary>
    private static void CheckTree(
        NodeSpec node,
        NodeScope scope,
        List<Error> errors)
    {
        // 单节点不变量由节点规则统一判定，这里只补跨节点与阶段规则
        string label = string.IsNullOrWhiteSpace(node.Label.Value) ? "(未命名)" : node.Label.Value;
        node.Check(NodeRuleContext.FlowDefinition, message =>
            errors.Add(scope.Node(label, message)));

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
            INodeType? definition = node.Type;
            NodeTypeShape? shape = definition?.Shape;

            CheckFromGroups(node, scope, errors);

            if (shape is null)
            {
                errors.Add(scope.Node(node.Label.Value, "执行节点必须写类型。"));
            }
            else if (shape is { Organizes: false })
            {
                definition!.Validate(node, scope, errors.Add);
            }
        }
        else if (node.Use is null)
        {
            errors.Add(scope.Node(node.Label.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>校验上游启动组：组覆盖不带默认值的输入端口、来源节点存在、端口存在、容器封装不泄漏。</summary>
    private static void CheckFromGroups(NodeSpec node, NodeScope scope, List<Error> errors)
    {
        if (node.Nodes is { Count: > 0 })
        {
            return;
        }

        IReadOnlyList<InputPort> inputs = node.DeclaredInputs;
        var required = new HashSet<PortName>([.. inputs
            .Where(port => !port.IsStatic && port.Default is null)
            .Select(port => port.Name)]);
        if (required.Count > 0 && node.From.Count == 0)
        {
            errors.Add(scope.Node(node.Label.Value, "声明动态输入端口的节点必须至少给出一组 From 启动条件。"));
        }

        foreach (FromGroup group in node.From)
        {
            var covered = new HashSet<PortName>();
            foreach (PortName input in group.Links.Select(link => link.Input))
            {
                if (node.FindInput(input) is { IsStatic: true })
                {
                    errors.Add(scope.Node(node.Label.Value,
                        $"From 不能连接静态输入端口 {input}，静态输入用 Static 绑定。"));
                }
                else if (node.FindInput(input) is null)
                {
                    errors.Add(scope.Node(node.Label.Value,
                        $"From 连接的目标输入端口 {input} 没有在节点上声明。"));
                }

                if (!covered.Add(input))
                {
                    errors.Add(scope.Node(node.Label.Value, $"From 组内的目标输入端口 {input} 出现多次。"));
                }
            }

            if (required.Count > 0)
            {
                foreach (PortName missing in required.Where(port => !covered.Contains(port)))
                {
                    errors.Add(scope.Node(node.Label.Value, $"From 组没有覆盖输入端口 {missing.Value}。"));
                }
            }
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

                if (node.OutputBindings?.TryGetValue(output.Name, out NodePort target) != true)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 没有解析到来源。"));
                    continue;
                }

                if (target.Node == node.Path)
                {
                    if (node.FindInput(target.Port) is not { IsStatic: true })
                    {
                        errors.Add(scope.Node(node.Path.Value,
                            $"输出端口 {output.Name.Value} 的来源 {target.Port.Value} 不是本节点的静态输入。"));
                    }

                    continue;
                }

                if (!target.Node.IsUnder(node.Path))
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的来源 {target.Node} 不在节点 {node.Path} 的子树里。"));
                    continue;
                }

                GraphNode bound = graph[target.Node];
                if (bound.FindPort(target.Port) is not { } memberPort)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的来源节点 {target.Node} 没有输出端口 {target.Port.Value}。"));
                    continue;
                }

                if (memberPort.IsConstant != output.IsConstant)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的常量性必须与来源 {target.Node}@{target.Port} 一致。"));
                }

                if (memberPort.Type != output.Type)
                {
                    errors.Add(scope.Node(node.Path.Value,
                        $"输出端口 {output.Name.Value} 的类型必须与来源 {target.Node}@{target.Port} 一致。"));
                }
            }
        }
    }

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
                    errors.Add(scope.Node(resolution.Node.Value, error));
                }
            }
        }
    }

    /// <summary>一条边的来源侧校验：来源端口必须声明存在且为动态端口，成员不能被容器外直接引用，来源类型与目标输入一致。</summary>
    private static void CheckEdgeSource(NodeGraph graph, FlowEdge edge, ExecutableNode target, NodeScope scope, List<Error> errors)
    {
        GraphNode source = graph[edge.Source];
        if (source.FindPort(edge.SourcePort) is not { } port)
        {
            errors.Add(scope.Node(target.Path.Value, $"节点 {edge.Source} 没有输出端口 {edge.SourcePort.Value}。"));
            return;
        }

        if (port.IsConstant)
        {
            errors.Add(scope.Node(target.Path.Value,
                $"常量输出端口 {source.Path}@{edge.SourcePort.Value} 不进入 From，使用 Static 绑定。"));
            return;
        }

        if (edge.Type == PortTypes.Context && !SupportsContext(graph, source, port))
        {
            errors.Add(scope.Node(target.Path.Value,
                $"Context 输入来源 {source.Path} 必须产出 Context 端口，实际来源端口 {edge.SourcePort.Value} 是 {port.Type}。"));
        }

        if (source is ExecutableNode executable && !Visible(executable.Path, target.Path))
        {
            errors.Add(scope.Node(target.Path.Value,
                $"不能从容器外直接引用容器内成员或端口 {executable.Path}，容器对外只暴露输出端口。"));
        }
    }

    /// <summary>来源是否能提供上下文：开始恒可提供，执行节点按类型的输出值域，容器按声明的输出端口。</summary>
    private static bool SupportsContext(NodeGraph graph, GraphNode source, Port port) => source switch
    {
        StartNode => true,
        ExecutableNode executable => graph.ShapeOf(executable.Path) is { Organizes: false } shape
            && shape.OutputTypes.Any(type => type.Semantics == PortSemantics.Context),
        ContainerNode => port.Type.Semantics == PortSemantics.Context,
        _ => false,
    };

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
        CheckReachability(graph, scope, errors);
        CheckGroupSources(graph, scope, errors);
        CheckOutputBindings(graph, scope, errors);
        CheckStaticBindings(graph, scope, errors);

        foreach (FlowEdge edge in graph.Edges)
        {
            if (graph[edge.Target] is not ExecutableNode target)
            {
                continue;
            }

            CheckEdgeSource(graph, edge, target, scope, errors);
        }
    }

    /// <summary>组间来源端口不重复：同一来源端口作为启动条件只能落在一个组里，跨组出现即多组同时齐备。</summary>
    private static void CheckGroupSources(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (IGrouping<(NodePath Target, NodePath Source, PortName SourcePort), FlowEdge> group in graph.Edges
            .GroupBy(edge => (edge.Target, edge.Source, edge.SourcePort)))
        {
            if (group.Select(edge => edge.Group).Distinct().Count() < 2
                || graph[group.Key.Target] is not ExecutableNode target)
            {
                continue;
            }

            errors.Add(scope.Node(target.Path.Value, $"From 的来源端口 {group.Key.Source}@{group.Key.SourcePort} 出现在多个组里。"));
        }
    }

    /// <summary>可达性规则：开始是唯一外部来源，全部输入静态且没有入边的执行节点在任务启动即可启动；
    /// 其余执行节点必须能从这些初始来源沿引用边到达。容器是成员的汇合点，成员全部可达时容器才可达。</summary>
    private static void CheckReachability(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var reached = new HashSet<NodePath> { graph.Start.Path };
        var queue = new Queue<NodePath>();
        queue.Enqueue(graph.Start.Path);
        foreach (ExecutableNode executable in graph.ExecutableNodes
            .Where(node => node.Plan is { StartsAtTaskStart: true })
            .Where(node => reached.Add(node.Path)))
        {
            queue.Enqueue(executable.Path);
        }

        while (true)
        {
            // reached.Add 兼作去重判定：首次到达的节点才入队
            while (queue.TryDequeue(out NodePath current))
            {
                foreach (NodePath next in graph.Outgoing(current).Select(edge => edge.Target).Where(reached.Add))
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

        string entry = graph.Start.Outputs
            .Where(port => port.Value is null)
            .Select(port => $" {StartNode.ReservedLabel}@{port.Name}")
            .FirstOrDefault() ?? " 开始入口";
        foreach (ExecutableNode executable in graph.ExecutableNodes.Where(node => !reached.Contains(node.Path)))
        {
            errors.Add(scope.Node(executable.Path.Value, $"从开始不可达，不会被触发，请用 From 接到来源节点或{entry}。"));
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

        foreach (FlowEdge edge in graph.Edges)
        {
            foreach (NodePath source in graph.ExecutablesIn(edge.Source))
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

    /// <summary>引用环的启动不变量：环必须有环外来源或环内输入节点，否则环内节点永远等不到启动条件，任务无法推进。
    /// 开始是流程级来源，输入节点每轮从外部接收回答，两者都构成周期性外源。节点依赖构成的迭代环若可启动则合法。</summary>
    private static void CheckLoops(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (FlowEdge edge in graph.Edges)
        {
            // 成员从自身所在容器取输入会让容器永远等不到齐备，是启动条件永不满足的环
            if (graph[edge.Source] is ContainerNode container
                && graph.ExecutablesIn(container.Path).Contains(edge.Target))
            {
                errors.Add(scope.Node(graph[edge.Target].Path.Value,
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
                graph.Incoming(member).Any(edge =>
                    graph[edge.Source] is StartNode
                    || graph.ExecutablesIn(edge.Source).Any(source => !members.Contains(source))));
            if (!externalEntry)
            {
                ExecutableNode representative = (ExecutableNode)graph[members[0]];
                errors.Add(scope.Node(representative.Path.Value,
                    "引用环没有环外来源且环内没有输入节点，任务无法启动。"));
            }
        }
    }
}
