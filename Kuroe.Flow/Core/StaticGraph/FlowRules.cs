using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
namespace Kuroe.Flow.StaticGraph;

using Flow = Shared.StaticGraph;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
public static class FlowRules
{
    /// <summary>校验一条流程。类型规则由节点持有的类型描述承担。</summary>
    public static ErrorOr<Success> Validate(FlowDefinition flow)
    {
        List<Error> errors = [];

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

        // 引用解析在编译期进行，解析错误与展平后的图规则一并给出
        if (errors.Count == 0)
        {
            ErrorOr<NodeGraph> compiled = FlowCompiler.Compile(flow);
            if (compiled.IsError)
            {
                errors.AddRange(compiled.ErrorsOrEmptyList);
            }
            else
            {
                CheckShape(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>开始入口的静态规则：至少一个 Text 端口，恰好一个不带 Value 的端口承接提交目标，其余端口必须带 Value。</summary>
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
            if (port.Type != Flow.PortType.Text)
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

        if (flow.StartOutputs.Count(port => port.Value is null) != 1)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value,
                "开始入口必须恰好有一个不带 Value 的端口承接提交的任务目标。"));
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

    /// <summary>节点标识的规则：非空、不含 @ 与 /、不是流程起点的保留标识。
    /// 定义条目按节点名判定，引用条目按标签判定，两者共用一份判断。</summary>
    private static void CheckIdentity(
        string value,
        string kind,
        string reserved,
        string reservedKind,
        NodeScope scope,
        List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(scope.Node("(未命名)", $"{kind}不能为空。"));
        }
        else if (value == reserved)
        {
            errors.Add(scope.Node(value, $"{kind} {value} 是流程起点的保留{reservedKind}。"));
        }
        else if (value.Contains('@'))
        {
            errors.Add(scope.Node(value, $"{kind}不能含 @，@ 是端口引用的分隔符。"));
        }
        else if (value.Contains('/'))
        {
            errors.Add(scope.Node(value, $"{kind}不能含 /，/ 是节点路径的分隔符。"));
        }
    }

    /// <summary>递归校验标识、容器形状、模型选择与容器输出端口。引用解析与图规则在展平后的图上校验。</summary>
    private static void CheckTree(
        NodeSpec node,
        HashSet<ModelRef> modelNames,
        NodeScope scope,
        List<Error> errors)
    {
        CheckValidation(node, scope, errors);

        // 定义条目以节点名为准，引用条目以标签为准，两者都落成节点在图里的标识段
        if (node.Name is { } name)
        {
            CheckIdentity(name.Value, "节点名", StartNode.ReservedName.Value, "名", scope, errors);

            if (name.Value != node.Label.Value)
            {
                errors.Add(scope.Node(node.Label.Value,
                    $"定义条目的标签取节点名 {name.Value} 的文本，不能另写标识。"));
            }
        }
        else
        {
            CheckIdentity(node.Label.Value, "节点标签", StartNode.ReservedLabel.Value, "标签", scope, errors);
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            node.Type?.Validate(node, scope, errors.Add);
            CheckSiblings(children, scope, errors);

            if (node.Model is not null || node.Models is not null)
            {
                errors.Add(scope.Node(node.Label.Value, "容器节点不是执行节点，不声明模型。"));
            }

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

            if (node.From.Count > 0)
            {
                errors.Add(scope.Node(node.Label.Value, "容器节点不是执行节点，不支持 From。"));
            }
        }
        else if (node.Execution is not null)
        {
            INodeType? definition = node.Type;
            NodeTypeShape? shape = definition?.Shape;

            CheckFromGroups(node, scope, errors);

            if (shape is null)
            {
                errors.Add(scope.Node(node.Label.Value, "执行节点必须写类型。"));
            }
            else
            {
                // 不派发 run 的类型不需要模型选择
                if (shape.DispatchesRuns)
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
                ValidateInterface(node, shape, scope, errors);
            }
        }
        else if (node.Use is null)
        {
            errors.Add(scope.Node(node.Label.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>校验上游启动组：组覆盖声明的输入端口、来源节点存在、端口存在、容器封装不泄漏。</summary>
    private static void CheckFromGroups(NodeSpec node, NodeScope scope, List<Error> errors)
    {
        if (node.Nodes is { Count: > 0 })
        {
            return;
        }

        var declared = new HashSet<PortName>([.. node.Inputs.Select(port => port.Name)]);
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

            if (declared.Count > 0)
            {
                foreach (PortName missing in declared.Where(port => !covered.Contains(port)))
                {
                    errors.Add(scope.Node(node.Label.Value, $"From 组没有覆盖输入端口 {missing.Value}。"));
                }
            }
        }
    }

    /// <summary>容器输出端口绑定与来源端口在展平后的图上校验：绑定目标必须是容器子树内声明该端口的执行节点，容器不透出 WorkItems 端口。</summary>
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

                if (member.FindPort(target.Port) is not { } memberPort)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输出端口 {port.Value} 的绑定目标节点 {target.Node} 没有输出端口 {target.Port.Value}。"));
                    continue;
                }

                if (memberPort.Type == Flow.PortType.WorkItems)
                {
                    errors.Add(scope.Node(container.Path.Value,
                        $"容器输出端口 {port.Value} 的绑定目标是 WorkItems 端口，容器不透出 WorkItems 端口。"));
                }
            }
        }
    }

    /// <summary>一条边的来源侧校验：来源端口必须声明存在，容器成员不能被容器外直接引用，Context 输入来源必须是产出上下文的非按条目执行节点。</summary>
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

            if (edge.Type == Flow.PortType.Context)
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

        if (edge.Type == Flow.PortType.Context)
        {
            CheckContextSource(graph, executable, target, scope, errors);
        }
    }

    /// <summary>Context 输入的来源规则：必须是非输入执行节点，产出 Context 端口，且不按条目展开。</summary>
    private static void CheckContextSource(NodeGraph graph, ExecutableNode source, ExecutableNode target, NodeScope scope, List<Error> errors)
    {
        if (graph.ShapeOf(source.Path) is { Organizes: false } shape && !shape.OutputTypes.Contains(Flow.PortType.Context))
        {
            errors.Add(scope.Node(target.Path.Value, $"Context 输入来源 {source.Path} 不产出上下文端口。"));
            return;
        }

        if (source.Execution.Mode == Flow.NodeMode.PerItem)
        {
            errors.Add(scope.Node(target.Path.Value, $"Context 输入来源 {source.Path} 不能是按条目展开的节点。"));
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
            if (node.Execution is not null)
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

    /// <summary>节点接口的静态规则：端口名与端口类型，端口值域与 Question 归属按类型形状判定。</summary>
    private static void ValidateInterface(NodeSpec node, NodeTypeShape shape, NodeScope scope, List<Error> errors) =>
        NodeInterfaceRules.Check(node, shape, message => errors.Add(scope.Node(node.Label.Value, message)));

    /// <summary>输出校验的取值边界：参数必要性、正则合法性。</summary>
    private static void CheckValidation(NodeSpec node, NodeScope scope, List<Error> errors)
    {
        OutputValidation? validation = node.Execution?.Validate;
        if (validation is null)
        {
            return;
        }

        switch (validation.Predicate)
        {
            case Flow.ValidationPredicate.NonEmpty:
                if (validation.Argument is not null)
                {
                    errors.Add(scope.Node(node.Label.Value, "NonEmpty 校验不接受参数。"));
                }

                break;

            case Flow.ValidationPredicate.Pattern:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(scope.Node(node.Label.Value, "Pattern 校验需要正则参数。"));
                }
                else
                {
                    try
                    {
                        _ = new System.Text.RegularExpressions.Regex(
                            validation.Argument, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch (System.Text.RegularExpressions.RegexParseException)
                    {
                        errors.Add(scope.Node(node.Label.Value, $"Pattern 校验的正则不合法：{validation.Argument}。"));
                    }
                }

                break;

            default:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(scope.Node(node.Label.Value, $"{validation.Predicate} 校验需要指定文本参数。"));
                }

                break;
        }
    }

    /// <summary>展平后的图规则：见环合法性、从开始的可达性、逐条对齐的形态与组间来源端口不重复。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        CheckLoops(graph, scope, errors);
        CheckAlignment(graph, scope, errors);
        CheckReachability(graph, scope, errors);
        CheckGroupSources(graph, scope, errors);
        CheckContainerBindings(graph, scope, errors);

        foreach (FlowEdge edge in graph.Edges)
        {
            if (graph[edge.Target] is not ExecutableNode target)
            {
                continue;
            }

            GraphNode source = graph[edge.Source];
            CheckEdgeSource(graph, edge, target, scope, errors);

            if (target.Execution.Mode == Flow.NodeMode.Single
                && source is ExecutableNode { Execution.Mode: Flow.NodeMode.PerItem }
                && target.HasWorkItemsPort)
            {
                errors.Add(scope.Node(target.Label.Value, "声明 WorkItems 端口的执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (graph.TypeOf(edge.Source, edge.SourcePort) == Flow.PortType.WorkItems
                && edge.Role != EdgeKind.Split
                && edge.Type != Flow.PortType.Signal)
            {
                errors.Add(scope.Node(target.Label.Value, "WorkItems 端口只能被按条目展开的执行节点消费，或作 Signal 驱动。"));
            }

            if (edge.Scope == FeedScope.Item
                && graph.ItemSpace(edge.Source) is { } fromSpace
                && graph.ItemSpace(edge.Target) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(scope.Node(target.Label.Value, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Execution.Branch is { } branch
                && (target.Execution.Mode != Flow.NodeMode.PerItem || graph.PlanOf(target.Path)?.SplitSource is null))
            {
                errors.Add(scope.Node(target.Path.Value, $"声明分支 {branch} 的执行节点必须按条目展开并从声明 WorkItems 端口的执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从声明 WorkItems 端口的执行节点取拆分，或从其它展开执行节点取对齐
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Execution.Mode != Flow.NodeMode.PerItem)
            {
                continue;
            }

            int splitSources = graph.Incoming(executable.Path)
                .Where(edge => edge.Role == EdgeKind.Split)
                .Select(edge => edge.Source)
                .Distinct()
                .Count();
            if (splitSources > 1)
            {
                errors.Add(scope.Node(executable.Path.Value, "按条目展开的执行节点只能有一个拆分来源，多个来源的条目无法归属同一个实例空间。"));
            }

            bool expandable = executable.Incoming.Any(edge =>
                edge.Role == EdgeKind.Split || edge.Scope == FeedScope.Item);
            if (!expandable)
            {
                // 容器来源只供整份上下文，不提供实例集，单独出现时节点永远无法展开
                string reason = executable.Incoming.Any(edge => graph[edge.Source] is ContainerNode)
                    ? "按条目展开的执行节点不能从容器取实例集，请引用声明 WorkItems 端口的执行节点或其它展开执行节点。"
                    : "按条目展开的执行节点必须从声明 WorkItems 端口的执行节点或其它展开执行节点取输入。";
                errors.Add(scope.Node(executable.Path.Value, reason));
            }
        }
    }

    /// <summary>组间来源端口不重复：同一来源端口作为启动条件只能落在一个组里，跨组出现即多组同时齐备。
    /// 为按条目目标提供实例空间的连接不驱动启动，每个组都要写它，不受此限。</summary>
    private static void CheckGroupSources(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (IGrouping<(NodePath Target, NodePath Source, PortName SourcePort), FlowEdge> group in graph.Edges
            .Where(edge => edge.Role != EdgeKind.Split)
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

    /// <summary>引用环的启动不变量：环必须有环外来源或环内输入节点，否则环内节点永远等不到启动条件，任务无法推进。
    /// 开始是流程级来源，输入节点每轮从外部接收回答，两者都构成周期性外源。整节点依赖构成的迭代环若可启动则合法。</summary>
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

        foreach (NodePath[] members in graph.Loops())
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

    /// <summary>逐条对齐的引用必须无环：按条目展开的实例空间沿对齐来源递推，环里的对齐会让它递归不止。
    /// 整节点依赖构成的环由运行时自然迭代，不在此拒绝。</summary>
    private static void CheckAlignment(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var indegree = new Dictionary<NodePath, int>();
        var outgoing = new Dictionary<NodePath, List<NodePath>>();
        foreach (NodePath path in graph.ExecutableNodes.Select(node => node.Path))
        {
            indegree[path] = 0;
            outgoing[path] = [];
        }

        foreach (FlowEdge edge in graph.Edges.Where(edge => edge.Role == EdgeKind.Split || edge.Scope == FeedScope.Item))
        {
            foreach (NodePath source in graph.ExecutablesIn(edge.Source))
            {
                indegree[edge.Target]++;
                outgoing[source].Add(edge.Target);
            }
        }

        var ready = new Queue<NodePath>(indegree.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        int visited = 0;
        while (ready.TryDequeue(out NodePath current))
        {
            visited++;
            foreach (NodePath to in outgoing[current])
            {
                if (--indegree[to] == 0)
                {
                    ready.Enqueue(to);
                }
            }
        }

        if (visited < graph.ExecutableNodes.Count)
        {
            errors.Add(FlowErrors.Body(scope.Name, "逐条对齐的引用关系存在环，请检查 From。"));
        }
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static FlowName? Duplicated(IReadOnlyList<FlowDefinition> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}
