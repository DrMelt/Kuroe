using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
namespace Kuroe.Flow.StaticGraph;

using Flow = Shared.StaticGraph;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
public static class FlowRules
{
    /// <summary>校验一条流程。类型规则由注册表给出的节点类型定义承担。</summary>
    public static ErrorOr<Success> Validate(FlowDefinition flow, NodeTypeRegistry types)
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

        var containerNames = new HashSet<NodeName>();
        CollectContainers(flow.RootNode, containerNames);

        CheckStart(flow, errors);

        var executableNames = new HashSet<NodeName>();
        CollectExecutables(flow.RootNode, executableNames);

        var contextlessNames = new HashSet<NodeName>();
        CollectContextless(flow.RootNode, contextlessNames, types);

        var perItemNames = new HashSet<NodeName>();
        CollectPerItem(flow.RootNode, perItemNames);

        var names = new HashSet<NodeName>();
        var outputs = new Dictionary<NodeName, IReadOnlyList<Port>>();
        CollectOutputs(flow.RootNode, outputs);
        outputs[StartNode.ReservedName] = flow.StartOutputs;

        var containerOutputs = new Dictionary<NodeName, IReadOnlyDictionary<PortName, ContainerOutput>>();
        CollectContainerOutputs(flow.RootNode, containerOutputs);

        var parentOf = new Dictionary<NodeName, NodeName>();
        CollectParents(flow.RootNode, parentOf, parent: null);

        CheckTree(flow.RootNode, names, executableNames, contextlessNames, perItemNames, modelNames, containerNames, outputs, containerOutputs, parentOf, types, NodeScope.Flow(flow.Name.Value), errors);

        // 引用类错误不存在时才展平，避免编译时的模型选择查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow, types), NodeScope.Flow(flow.Name.Value), errors);
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

    /// <summary>收集全部容器名，From 引用允许指向容器。</summary>
    private static void CollectContainers(NodeSpec node, HashSet<NodeName> names)
    {
        if (node.Nodes is not { Count: > 0 } children)
        {
            return;
        }

        names.Add(node.Name);
        foreach (NodeSpec child in children)
        {
            CollectContainers(child, names);
        }
    }

    /// <summary>收集执行节点名，From 引用据此判定存在。</summary>
    private static void CollectExecutables(NodeSpec node, HashSet<NodeName> names)
    {
        if (node.Execution is not null)
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                CollectExecutables(child, names);
            }
        }
    }

    /// <summary>收集不产出上下文端口的节点名，Context 输入来源据此拦截。</summary>
    private static void CollectContextless(NodeSpec node, HashSet<NodeName> names, NodeTypeRegistry types)
    {
        if (ShapeOf(node, types) is { Organizes: false } shape
            && !shape.OutputTypes.Contains(PortType.Context))
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                CollectContextless(child, names, types);
            }
        }
    }

    /// <summary>节点声明的类型形状，引用节点与未注册的类型为空。</summary>
    private static NodeTypeShape? ShapeOf(NodeSpec node, NodeTypeRegistry types) =>
        node.Kind is { } kind ? types.Create(kind)?.Shape : null;

    /// <summary>收集按条目展开的执行节点名，上下文输入端口来源据此拦截。</summary>
    private static void CollectPerItem(NodeSpec node, HashSet<NodeName> names)
    {
        if (node.Execution is { Mode: Flow.NodeMode.PerItem })
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                CollectPerItem(child, names);
            }
        }
    }

    /// <summary>收集执行节点的输出端口表，From 引用据此判定端口存在与类型。</summary>
    private static void CollectOutputs(NodeSpec node, Dictionary<NodeName, IReadOnlyList<Port>> outputs)
    {
        if (node.Execution is not null)
        {
            outputs[node.Name] = node.Outputs;
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                CollectOutputs(child, outputs);
            }
        }
    }

    /// <summary>收集容器实例的输出端口表，装配层 From 引用容器端口据此判定端口存在。</summary>
    private static void CollectContainerOutputs(
        NodeSpec node,
        Dictionary<NodeName, IReadOnlyDictionary<PortName, ContainerOutput>> containerOutputs)
    {
        if (node.Nodes is not { Count: > 0 } children)
        {
            return;
        }

        if (node.ContainerOutputs is { Count: > 0 } outs)
        {
            containerOutputs[node.Name] = outs;
        }

        foreach (NodeSpec child in children)
        {
            CollectContainerOutputs(child, containerOutputs);
        }
    }

    /// <summary>收集每个节点的直接所属容器名，容器封装据此判定引用作用域。</summary>
    private static void CollectParents(
        NodeSpec node,
        Dictionary<NodeName, NodeName> parentOf,
        NodeName? parent)
    {
        if (parent is { } container)
        {
            parentOf[node.Name] = container;
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                CollectParents(child, parentOf, node.Name);
            }
        }
    }

    /// <summary>引用来源是否落在当前节点可见的容器作用域内：同层成员或外层容器成员可见，嵌套更深容器的成员对容器外不可见。
    /// 引用者本身没有父容器时只允许引用同根下节点。</summary>
    private static bool IsInScope(
        NodeName source,
        NodeName referrer,
        IReadOnlyDictionary<NodeName, NodeName> parentOf)
    {
        if (!parentOf.TryGetValue(source, out NodeName sourceContainer))
        {
            return false;
        }

        if (!parentOf.TryGetValue(referrer, out NodeName referrerContainer))
        {
            return false;
        }

        // 沿引用者所在容器链上溯，遇到来源所在容器即可见
        NodeName? current = referrerContainer;
        while (current is { } container)
        {
            if (container == sourceContainer)
            {
                return true;
            }

            current = parentOf.TryGetValue(container, out NodeName parent) ? parent : null;
        }

        return false;
    }

    /// <summary>递归校验名字、From 引用、容器与模型选择引用。执行先后由拓扑排序保证，这里只检查引用落在执行节点或容器上。</summary>
    private static void CheckTree(
        NodeSpec node,
        HashSet<NodeName> names,
        HashSet<NodeName> executableNames,
        HashSet<NodeName> contextlessNames,
        HashSet<NodeName> perItemNames,
        HashSet<ModelRef> modelNames,
        HashSet<NodeName> containerNames,
        IReadOnlyDictionary<NodeName, IReadOnlyList<Port>> outputs,
        IReadOnlyDictionary<NodeName, IReadOnlyDictionary<PortName, ContainerOutput>> containerOutputs,
        IReadOnlyDictionary<NodeName, NodeName> parentOf,
        NodeTypeRegistry types,
        NodeScope scope,
        List<Error> errors)
    {
        CheckSourceReferences(node, executableNames, contextlessNames, perItemNames, containerNames, outputs, containerOutputs, parentOf, scope, errors);

        CheckValidation(node, scope, errors);

        if (string.IsNullOrWhiteSpace(node.Name.Value))
        {
            errors.Add(scope.Node("(未命名)", "节点名不能为空。"));
        }
        else if (node.Name == StartNode.ReservedName)
        {
            errors.Add(scope.Node(node.Name.Value, $"节点名 {StartNode.ReservedName} 是流程起点的保留名。"));
        }
        else if (node.Name.Value.Contains('@'))
        {
            errors.Add(scope.Node(node.Name.Value, "节点名不能含 @，@ 是端口引用的分隔符。"));
        }
        else if (!names.Add(node.Name))
        {
            errors.Add(scope.Node(node.Name.Value, "节点名重复。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (node.Kind is { } containerKind && types.Create(containerKind) is { } containerType)
            {
                containerType.Validate(node, scope, errors.Add);
            }

            if (node.Model is not null || node.Models is not null)
            {
                errors.Add(scope.Node(node.Name.Value, "容器节点不是执行节点，不声明模型。"));
            }

            if (!HasExecutable(children))
            {
                errors.Add(scope.Node(node.Name.Value, "容器里至少要有一个执行节点。"));
            }
            else
            {
                foreach (NodeSpec child in children)
                {
                    CheckTree(child, names, executableNames, contextlessNames, perItemNames, modelNames, containerNames, outputs, containerOutputs, parentOf, types, scope, errors);
                }
            }

            if (node.ContainerOutputs is { } outs)
            {
                CheckContainerOut(node, outs, children, outputs, scope, errors);
            }

            if (node.From.Count > 0)
            {
                errors.Add(scope.Node(node.Name.Value, "容器节点不是执行节点，不支持 From。"));
            }
        }
        else if (node.Execution is not null)
        {
            INodeType? definition = node.Kind is { } declared ? types.Create(declared) : null;
            NodeTypeShape? shape = definition?.Shape;

            if (shape is null)
            {
                errors.Add(scope.Node(node.Name.Value, $"节点类型 {node.Kind?.Value ?? "(未写)"} 未注册。"));
            }
            else
            {
                // 不派发 run 的类型不需要模型选择
                if (shape.DispatchesRuns)
                {
                    if (node.Model is not { } model)
                    {
                        errors.Add(scope.Node(node.Name.Value, "执行节点必须声明模型选择。"));
                    }
                    else if (!modelNames.Contains(model))
                    {
                        errors.Add(scope.Node(node.Name.Value, $"引用的模型选择 {model.Value} 不存在。"));
                    }
                }

                definition!.Validate(node, scope, errors.Add);
                ValidateInterface(node, shape, scope, errors);
            }
        }
        else if (node.Use is null)
        {
            errors.Add(scope.Node(node.Name.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>校验上游启动组：组覆盖声明的输入端口、来源节点存在、端口存在、容器封装不泄漏。</summary>
    private static void CheckSourceReferences(
        NodeSpec node,
        HashSet<NodeName> executableNames,
        HashSet<NodeName> contextlessNames,
        HashSet<NodeName> perItemNames,
        HashSet<NodeName> containerNames,
        IReadOnlyDictionary<NodeName, IReadOnlyList<Port>> outputs,
        IReadOnlyDictionary<NodeName, IReadOnlyDictionary<PortName, ContainerOutput>> containerOutputs,
        IReadOnlyDictionary<NodeName, NodeName> parentOf,
        NodeScope scope,
        List<Error> errors)
    {
        if (node.Nodes is { Count: > 0 })
        {
            return;
        }

        var declared = new HashSet<PortName>([.. node.Inputs.Select(port => port.Name)]);
        if (declared.Count > 0 && node.From.Count == 0)
        {
            errors.Add(scope.Node(node.Name.Value, "声明输入端口的节点必须至少给出一组 From 启动条件。"));
        }

        foreach (FromGroup group in node.From)
        {
            var covered = new HashSet<PortName>();
            foreach (PortName input in group.Links.Select(link => link.Input))
            {
                if (declared.Count > 0 && !declared.Contains(input))
                {
                    errors.Add(scope.Node(node.Name.Value,
                        $"From 连接的目标输入端口 {input} 没有在节点上声明。"));
                }

                if (!covered.Add(input))
                {
                    errors.Add(scope.Node(node.Name.Value, $"From 组内的目标输入端口 {input} 出现多次。"));
                }
            }

            if (declared.Count > 0)
            {
                foreach (PortName missing in declared.Where(port => !covered.Contains(port)))
                {
                    errors.Add(scope.Node(node.Name.Value, $"From 组没有覆盖输入端口 {missing.Value}。"));
                }
            }
        }

        foreach (FromLink link in node.From.SelectMany(group => group.Links))
        {
            PortRef reference = link.Ref;
            PortType type = node.Inputs.Find(link.Input)?.Type ?? Flow.PortType.Text;

            if (type == Flow.PortType.Context)
            {
                if (reference.Source is null)
                {
                    errors.Add(scope.Node(node.Name.Value,
                        "Context 输入必须写节点名或 来源@端口，不能引用绑定端口。"));
                }
                else
                {
                    NodeName contextSource = reference.Source.Value;
                    if (!executableNames.Contains(contextSource))
                    {
                        errors.Add(scope.Node(node.Name.Value,
                            $"Context 输入来源 {contextSource} 必须是非输入执行节点。"));
                    }
                    else if (contextlessNames.Contains(contextSource))
                    {
                        errors.Add(scope.Node(node.Name.Value,
                            $"Context 输入来源 {contextSource} 不产出上下文端口。"));
                    }
                    else if (perItemNames.Contains(contextSource))
                    {
                        errors.Add(scope.Node(node.Name.Value,
                            $"Context 输入来源 {contextSource} 不能是按条目展开的节点。"));
                    }
                }
            }

            if (reference.Source is null)
            {
                // 装配层执行节点没有传入端口可绑定，绑定端口引用在 From 里不合法
                errors.Add(scope.Node(node.Name.Value,
                    $"From 引用的节点 @{reference.Port.Value} 不在流程里。"));
                continue;
            }

            NodeName source = reference.Source.Value;

            // 开始是流程级来源，不受容器封装限制
            if (source != StartNode.ReservedName)
            {
                if (containerNames.Contains(source))
                {
                    // 容器对外只经 Outputs 声明端口，没有整份产出
                    if (!containerOutputs.TryGetValue(source, out IReadOnlyDictionary<PortName, ContainerOutput>? containerPorts)
                        || !containerPorts.ContainsKey(reference.Port))
                    {
                        errors.Add(scope.Node(node.Name.Value,
                            $"容器 {source} 没有输出端口 {reference.Port.Value}。"));
                    }

                    continue;
                }

                if (!executableNames.Contains(source))
                {
                    errors.Add(scope.Node(node.Name.Value, $"From 引用的节点 {source} 不在流程里。"));
                    continue;
                }

                if (!IsInScope(source, node.Name, parentOf))
                {
                    errors.Add(scope.Node(node.Name.Value,
                        $"不能从容器外直接引用容器内成员或端口 {source}，容器对外只暴露输出端口。"));
                    continue;
                }
            }

            if (!outputs.TryGetValue(source, out IReadOnlyList<Port>? declaredOutputs)
                || declaredOutputs.Find(reference.Port) is null)
            {
                errors.Add(scope.Node(node.Name.Value, $"节点 {source} 没有输出端口 {reference.Port.Value}。"));
            }
        }
    }

    /// <summary>装配容器输出端口的绑定校验：绑定目标必须在子树内且是执行节点，绑定的端口必须是目标成员声明的端口。装配层容器对外只经 Outputs 端口被消费，绑定错误会在引用处落空。</summary>
    private static void CheckContainerOut(
        NodeSpec container,
        IReadOnlyDictionary<PortName, ContainerOutput> outs,
        IReadOnlyList<NodeSpec> children,
        IReadOnlyDictionary<NodeName, IReadOnlyList<Port>> outputs,
        NodeScope scope,
        List<Error> errors)
    {
        foreach ((PortName port, ContainerOutput output) in outs)
        {
            PortRef target = output.Ref;
            if (target.Source is not { } sourceName)
            {
                errors.Add(scope.Node(container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标不能是对绑定端口的引用。"));
                continue;
            }

            if (!ContainsName(children, sourceName))
            {
                errors.Add(scope.Node(container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {sourceName} 不在容器 {container.Name} 的子节点里。"));
                continue;
            }

            if (!outputs.TryGetValue(sourceName, out IReadOnlyList<Port>? memberPorts))
            {
                errors.Add(scope.Node(container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标节点 {sourceName} 不是执行节点，无法取产出。"));
                continue;
            }

            if (memberPorts.Find(target.Port) is not { } memberPort)
            {
                errors.Add(scope.Node(container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标节点 {sourceName} 没有输出端口 {target.Port.Value}。"));
                continue;
            }

            if (memberPort.Type == Flow.PortType.WorkItems)
            {
                errors.Add(scope.Node(container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标是 WorkItems 端口，容器不透出 WorkItems 端口。"));
            }
        }
    }

    /// <summary>名字是否出现在节点表里，执行与容器都算。</summary>
    private static bool ContainsName(IReadOnlyList<NodeSpec> nodes, NodeName name)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node.Name == name)
            {
                return true;
            }

            if (node.Nodes is { Count: > 0 } children && ContainsName(children, name))
            {
                return true;
            }
        }

        return false;
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
        NodeInterfaceRules.Check(node, shape, message => errors.Add(scope.Node(node.Name.Value, message)));

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
                    errors.Add(scope.Node(node.Name.Value, "NonEmpty 校验不接受参数。"));
                }

                break;

            case Flow.ValidationPredicate.Pattern:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(scope.Node(node.Name.Value, "Pattern 校验需要正则参数。"));
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
                        errors.Add(scope.Node(node.Name.Value, $"Pattern 校验的正则不合法：{validation.Argument}。"));
                    }
                }

                break;

            default:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(scope.Node(node.Name.Value, $"{validation.Predicate} 校验需要指定文本参数。"));
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

        foreach (FlowEdge edge in graph.Edges)
        {
            GraphNode source = graph[edge.Source];
            if (graph[edge.Target] is not ExecutableNode target)
            {
                continue;
            }

            if (target.Execution.Mode == Flow.NodeMode.Single
                && source is ExecutableNode { Execution.Mode: Flow.NodeMode.PerItem }
                && target.HasWorkItemsPort)
            {
                errors.Add(scope.Node(target.Name.Value, "声明 WorkItems 端口的执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (graph.TypeOf(edge.Source, edge.SourcePort) == Flow.PortType.WorkItems
                && edge.Role != EdgeKind.Split
                && edge.Type != Flow.PortType.Signal)
            {
                errors.Add(scope.Node(target.Name.Value, "WorkItems 端口只能被按条目展开的执行节点消费，或作 Signal 驱动。"));
            }

            if (edge.Scope == FeedScope.Item
                && graph.ItemSpace(edge.Source) is { } fromSpace
                && graph.ItemSpace(edge.Target) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(scope.Node(target.Name.Value, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Execution.Branch is { } branch
                && (target.Execution.Mode != Flow.NodeMode.PerItem || graph.PlanOf(target.Index)?.SplitSource is null))
            {
                errors.Add(scope.Node(target.Name.Value, $"声明分支 {branch} 的执行节点必须按条目展开并从声明 WorkItems 端口的执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从声明 WorkItems 端口的执行节点取拆分，或从其它展开执行节点取对齐
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Execution.Mode != Flow.NodeMode.PerItem)
            {
                continue;
            }

            int splitSources = graph.Incoming(executable.Index)
                .Where(edge => edge.Role == EdgeKind.Split)
                .Select(edge => edge.Source)
                .Distinct()
                .Count();
            if (splitSources > 1)
            {
                errors.Add(scope.Node(executable.Name.Value, "按条目展开的执行节点只能有一个拆分来源，多个来源的条目无法归属同一个实例空间。"));
            }

            bool expandable = graph.Incoming(executable.Index).Any(edge =>
                edge.Role == EdgeKind.Split || edge.Scope == FeedScope.Item);
            if (!expandable)
            {
                // 容器来源只供整份上下文，不提供实例集，单独出现时节点永远无法展开
                string reason = graph.Incoming(executable.Index).Any(edge => graph[edge.Source] is ContainerNode)
                    ? "按条目展开的执行节点不能从容器取实例集，请引用声明 WorkItems 端口的执行节点或其它展开执行节点。"
                    : "按条目展开的执行节点必须从声明 WorkItems 端口的执行节点或其它展开执行节点取输入。";
                errors.Add(scope.Node(executable.Name.Value, reason));
            }
        }
    }

    /// <summary>组间来源端口不重复：同一来源端口作为启动条件只能落在一个组里，跨组出现即多组同时齐备。
    /// 为按条目目标提供实例空间的连接不驱动启动，每个组都要写它，不受此限。</summary>
    private static void CheckGroupSources(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        foreach (IGrouping<(int Target, int Source, PortName SourcePort), FlowEdge> group in graph.Edges
            .Where(edge => edge.Role != EdgeKind.Split)
            .GroupBy(edge => (edge.Target, edge.Source, edge.SourcePort)))
        {
            if (group.Select(edge => edge.Group).Distinct().Count() < 2
                || graph[group.Key.Target] is not ExecutableNode target)
            {
                continue;
            }

            string reference = new PortRef(graph[group.Key.Source].Name, group.Key.SourcePort).Display;
            errors.Add(scope.Node(target.Name.Value, $"From 的来源端口 {reference} 出现在多个组里。"));
        }
    }

    /// <summary>可达性规则：开始是唯一来源，每个执行节点都必须能从开始沿引用边到达，否则永远不会被触发。
    /// 容器是成员的汇合点，成员与子容器全部可达时容器才可达，引用容器的下游随之可达。</summary>
    private static void CheckReachability(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var reached = new HashSet<int> { graph.Start.Index };
        var queue = new Queue<int>();
        queue.Enqueue(graph.Start.Index);
        while (true)
        {
            // reached.Add 兼作去重判定：首次到达的节点才入队
            while (queue.TryDequeue(out int current))
            {
                foreach (int next in graph.Outgoing(current).Select(edge => edge.Target).Where(reached.Add))
                {
                    queue.Enqueue(next);
                }
            }

            ContainerNode? closed = graph.Containers.FirstOrDefault(container =>
                !reached.Contains(container.Index)
                && container.Members.All(reached.Contains)
                && container.SubContainers.All(reached.Contains));
            if (closed is null)
            {
                break;
            }

            reached.Add(closed.Index);
            queue.Enqueue(closed.Index);
        }

        string entry = graph.Start.Outputs
            .Where(port => port.Value is null)
            .Select(port => $" {StartNode.ReservedName}@{port.Name}")
            .FirstOrDefault() ?? " 开始入口";
        foreach (ExecutableNode executable in graph.ExecutableNodes.Where(node => !reached.Contains(node.Index)))
        {
            errors.Add(scope.Node(executable.Name.Value, $"从开始不可达，不会被触发，请用 From 接到来源节点或{entry}。"));
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
                && graph.ExecutablesIn(container.Index).Contains(edge.Target))
            {
                errors.Add(scope.Node(graph[edge.Target].Name.Value,
                    "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"));
            }
        }

        foreach (int[] members in graph.Loops())
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
                errors.Add(scope.Node(representative.Name.Value,
                    "引用环没有环外来源且环内没有输入节点，任务无法启动。"));
            }
        }
    }

    /// <summary>逐条对齐的引用必须无环：按条目展开的实例空间沿对齐来源递推，环里的对齐会让它递归不止。
    /// 整节点依赖构成的环由运行时自然迭代，不在此拒绝。</summary>
    private static void CheckAlignment(NodeGraph graph, NodeScope scope, List<Error> errors)
    {
        var indegree = new Dictionary<int, int>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (int index in graph.ExecutableNodes.Select(node => node.Index))
        {
            indegree[index] = 0;
            outgoing[index] = [];
        }

        foreach (FlowEdge edge in graph.Edges.Where(edge => edge.Role == EdgeKind.Split || edge.Scope == FeedScope.Item))
        {
            foreach (int source in graph.ExecutablesIn(edge.Source))
            {
                indegree[edge.Target]++;
                outgoing[source].Add(edge.Target);
            }
        }

        var ready = new Queue<int>(indegree.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        int visited = 0;
        while (ready.TryDequeue(out int current))
        {
            visited++;
            foreach (int to in outgoing[current])
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
