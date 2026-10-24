using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Compiler.Compilation;

namespace Kuroe.Flow.Compiler.Validation;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
internal static class FlowRules
{
    /// <summary>校验一条流程并给出执行视图：模型规则、图编译与图规则按固定顺序执行。类型规则由节点持有的类型描述承担。</summary>
    internal static ErrorOr<NodeGraph> ValidateAndCompile(FlowDefinition flow)
    {
        List<Error> errors = [];
        ValidateModel(flow, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        // 引用解析在编译期进行，解析错误与展平后的图规则一并给出
        ErrorOr<NodeGraph> compiled = FlowCompilation.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        ValidateGraph(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);

        return errors.Count > 0 ? errors : compiled.Value;
    }

    /// <summary>模型层规则：开始入口、模型选择与节点树。引用解析与图规则在具体图上判定。</summary>
    internal static void ValidateModel(FlowDefinition flow, List<Error> errors)
    {
        var modelNames = new HashSet<ModelRef>();
        foreach (ModelRef name in flow.Models.Select(model => model.Name))
        {
            if (string.IsNullOrWhiteSpace(name.Value))
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, "(未命名)", "模型选择名不能为空。"));
            }
            else if (!modelNames.Add(name))
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, name.Value, "模型选择名重复。"));
            }
        }

        foreach (ModelDefinition model in flow.Models)
        {
            if (model.Runtime && model.Model is not null)
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, model.Name.Value, "运行时模型与固定模型不能同时声明。"));
            }
        }

        CheckStart(flow, errors);
        CheckTree(flow.RootNode, modelNames, NodeScope.Flow(flow.Name.Value), errors);
    }

    /// <summary>图层规则：环、可达性与边来源。</summary>
    internal static void ValidateGraph(NodeGraph graph, NodeScope scope, List<Error> errors) =>
        CheckShape(graph, scope, errors);

    /// <summary>开始入口的静态规则：至少一个 Text 端口，至少一个不带 Value 的端口承接提交输入，其余端口必须带 Value。</summary>
    private static void CheckStart(FlowDefinition flow, List<Error> errors)
    {
        if (flow.StartOutputs.Count == 0)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, "流程必须声明开始入口，至少一个 Text 输出端口。"));
            return;
        }

        var names = new HashSet<PortName>();
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

            if (port.Value is { } value && string.IsNullOrWhiteSpace(value))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 的 Value 不能为空。"));
            }
        }

        if (!flow.StartOutputs.Any(port => port.Value is null))
        {
            errors.Add(FlowErrors.Body(flow.Name.Value,
                "开始入口必须至少有一个不带 Value 的端口承接提交的任务输入。"));
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

    /// <summary>递归校验标识、容器形状、模型选择与容器输出端口。引用解析与图规则在展平后的图上校验。</summary>
    private static void CheckTree(
        NodeSpec node,
        HashSet<ModelRef> modelNames,
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

            if (!HasExecutable(children))
            {
                errors.Add(scope.Node(node.Label.Value, "容器里至少要有一个执行节点。"));
            }
            else
            {
                foreach (NodeSpec child in children)
                {
                    CheckTree(child, modelNames, scope, errors);
                }
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
                // 不派发执行的类型不需要模型选择
                if (shape.DispatchesExecutions)
                {
                    if (node.Model is not { } model)
                    {
                        errors.Add(scope.Node(node.Label.Value, "执行节点必须声明模型选择。"));
                    }
                    else if (!modelNames.Contains(model))
                    {
                        errors.Add(scope.Node(node.Label.Value, $"引用的模型选择 {model.Value} 不存在。"));
                    }
                }

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

        var declared = new HashSet<PortName>([.. node.Inputs.Select(port => port.Name)]);
        var required = new HashSet<PortName>([.. node.Inputs.Where(port => port.Default is null).Select(port => port.Name)]);
        if (declared.Count > 0 && node.From.Count == 0)
        {
            errors.Add(scope.Node(node.Label.Value, "声明输入端口的节点必须至少给出一组 From 启动条件。"));
        }

        foreach (FromGroup group in node.From)
        {
            var covered = new HashSet<PortName>();
            foreach (PortName input in group.Links.Select(link => link.Input))
            {
                if (declared.Count > 0 && !declared.Contains(input))
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

    /// <summary>容器输出端口绑定与来源端口在展平后的图上校验：绑定目标必须是容器子树内声明该端口的执行节点。</summary>
    private static void CheckContainerBindings(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (ContainerNode container in graph.Containers)
        {
            if (container.PortBindings is not { Count: > 0 } bindings)
            {
                continue;
            }

            foreach ((PortName port, NodePort target) in bindings)
            {
                if (graph[target.Node] is not ExecutableNode member)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输出端口 {port.Value} 的绑定目标 {target.Node} 不是执行节点，无法取产出。"));
                    continue;
                }

                if (!target.Node.IsUnder(container.Path))
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输出端口 {port.Value} 的绑定目标 {target.Node} 不在容器 {container.Path} 的子树里。"));
                    continue;
                }

                if (member.FindPort(target.Port) is null)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输出端口 {port.Value} 的绑定目标节点 {target.Node} 没有输出端口 {target.Port.Value}。"));
                }
            }
        }
    }

    /// <summary>一条边的来源侧校验：来源端口必须声明存在，容器成员不能被容器外直接引用，Context 输入来源必须产出上下文端口。</summary>
    private static void CheckEdgeSource(NodeGraph graph, FlowEdge edge, ExecutableNode target, NodeScope scope, List<Error> errors)
    {
        GraphNode source = graph[edge.Source];

        // 开始是流程级来源，不受容器封装限制
        if (source is StartNode start)
        {
            if (start.FindPort(edge.SourcePort) is null)
            {
                errors.Add(scope.Node(target.Path.Value, $"节点 {StartNode.ReservedLabel} 没有输出端口 {edge.SourcePort.Value}。"));
            }

            return;
        }

        // 容器对外只经 Outputs 声明端口，没有整份产出
        if (source is ContainerNode container)
        {
            if (container.PortBindings?.ContainsKey(edge.SourcePort) != true)
            {
                errors.Add(scope.Node(target.Path.Value, $"容器 {container.Path} 没有输出端口 {edge.SourcePort.Value}。"));
            }

            if (edge.Type == PortTypes.Context)
            {
                errors.Add(scope.Node(target.Path.Value, $"Context 输入来源 {container.Path} 必须是非输入执行节点。"));
            }

            return;
        }

        if (source is not ExecutableNode executable)
        {
            return;
        }

        if (executable.FindPort(edge.SourcePort) is null)
        {
            errors.Add(scope.Node(target.Path.Value, $"节点 {executable.Path} 没有输出端口 {edge.SourcePort.Value}。"));
        }

        if (!Visible(executable.Path, target.Path))
        {
            errors.Add(scope.Node(target.Path.Value,
                $"不能从容器外直接引用容器内成员或端口 {executable.Path}，容器对外只暴露输出端口。"));
        }

        if (edge.Type == PortTypes.Context)
        {
            CheckContextSource(graph, executable, target, scope, errors);
        }
    }

    /// <summary>Context 输入的来源规则：必须是非输入执行节点，产出 Context 端口。</summary>
    private static void CheckContextSource(NodeGraph graph, ExecutableNode source, ExecutableNode target, NodeScope scope, List<Error> errors)
    {
        if (graph.ShapeOf(source.Path) is { Organizes: false } shape && !shape.OutputTypes.Contains(PortTypes.Context))
        {
            errors.Add(scope.Node(target.Path.Value, $"Context 输入来源 {source.Path} 不产出上下文端口。"));
        }
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

    /// <summary>子树里是否至少有一个执行节点。</summary>
    private static bool HasExecutable(IReadOnlyList<NodeSpec> nodes)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node.Spec is not null)
            {
                return true;
            }

            if (node.Nodes is { Count: > 0 } children && HasExecutable(children))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>展平后的图规则：见环合法性、从开始的可达性与组间来源端口不重复。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        CheckLoops(graph, scope, errors);
        CheckReachability(graph, scope, errors);
        CheckGroupSources(graph, scope, errors);
        CheckContainerBindings(graph, scope, errors);

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

    /// <summary>可达性规则：开始是唯一来源，每个执行节点都必须能从开始沿引用边到达，否则永远不会被触发。
    /// 容器是成员的汇合点，成员与子容器全部可达时容器才可达，引用容器的下游随之可达。</summary>
    private static void CheckReachability(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var reached = new HashSet<NodePath> { graph.Start.Path };
        var queue = new Queue<NodePath>();
        queue.Enqueue(graph.Start.Path);
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
